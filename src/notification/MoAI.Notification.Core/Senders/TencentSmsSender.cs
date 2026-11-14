using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Notification.Models;

namespace MoAI.Notification.Senders;

/// <summary>
/// 腾讯云短信发送器（SendSms）：平台侧已审核模板 Id + 参数发送，收件人取消息动态手机号列表.
/// <para>签名按腾讯云 API 3.0 TC3-HMAC-SHA256 标准流程实现；TemplateParamSet 为位置数组，
/// 取模板正文占位符出现顺序（SmsTemplateParamNames）对应的变量值；短信渠道必须使用通知模板触发.</para>
/// </summary>
public class TencentSmsSender : INotificationChannelSender
{
    private const string Service = "sms";
    private const string ApiVersion = "2021-01-11";

    /// <summary>
    /// Initializes a new instance of the <see cref="TencentSmsSender"/> class.
    /// </summary>
    /// <param name="httpClientFactory">HTTP 客户端工厂.</param>
    public TencentSmsSender(IHttpClientFactory httpClientFactory)
    {
        HttpClientFactory = httpClientFactory;
    }

    private IHttpClientFactory HttpClientFactory { get; }

    /// <inheritdoc/>
    public NotificationChannelType Type => NotificationChannelType.TencentSms;

    /// <inheritdoc/>
    public async Task SendAsync(NotificationChannelEntity channel, NotificationMessage message, CancellationToken cancellationToken)
    {
        var config = NotificationChannelConfigs.Deserialize<TencentSmsConfig>(channel.Config);
        var templateId = message.SmsTencentTemplateId;
        if (string.IsNullOrWhiteSpace(templateId))
        {
            throw new BusinessException("腾讯云短信渠道必须使用已配置腾讯云模板 Id 的通知模板发送") { StatusCode = 400 };
        }

        var phones = message.PhoneNumbers?
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .Distinct()
            .ToList();
        if (phones == null || phones.Count == 0)
        {
            throw new BusinessException("腾讯云短信渠道投递失败：未指定收件人手机号（收件人由发送时动态提供）") { StatusCode = 400 };
        }

        var payload = new Dictionary<string, object>
        {
            ["PhoneNumberSet"] = phones,
            ["SmsSdkAppId"] = config.SmsSdkAppId,
            ["SignName"] = config.SignName,
            ["TemplateId"] = templateId
        };

        // TemplateParamSet 顺序必须与平台模板中变量出现顺序一致
        var paramNames = message.SmsTemplateParamNames ?? new List<string>();
        if (paramNames.Count > 0)
        {
            var values = message.TemplateVariables ?? new Dictionary<string, string>();
            payload["TemplateParamSet"] = paramNames.Select(name => values.TryGetValue(name, out var value) ? value : string.Empty).ToArray();
        }

        var endpoint = config.Endpoint.TrimEnd('/');
        var host = new Uri(endpoint).Host;
        var body = JsonSerializer.Serialize(payload, NotificationChannelConfigs.JsonOptions);
        var authorization = Sign(config, host, "SendSms", body, out var timestamp);

        var headers = new Dictionary<string, string>
        {
            ["Authorization"] = authorization,
            ["Content-Type"] = "application/json; charset=utf-8",
            ["X-TC-Action"] = "SendSms",
            ["X-TC-Version"] = ApiVersion,
            ["X-TC-Timestamp"] = timestamp.ToString(CultureInfo.InvariantCulture),
            ["X-TC-Region"] = "ap-guangzhou"
        };

        var (statusCode, responseBody) = await SenderHttp.PostRawAsync(HttpClientFactory, endpoint, body, "application/json; charset=utf-8", headers, cancellationToken);
        SenderHttp.EnsureSuccessStatus(statusCode, responseBody, "腾讯云短信");

        string? errorCode = null;
        string? errorMessage = null;
        JsonElement sendStatusSet = default;
        var hasSendStatusSet = false;
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            if (!doc.RootElement.TryGetProperty("Response", out var root))
            {
                throw new BusinessException($"腾讯云短信返回缺少 Response：{SenderHttp.TruncateBody(responseBody)}") { StatusCode = 502 };
            }

            if (root.TryGetProperty("Error", out var error))
            {
                errorCode = error.TryGetProperty("Code", out var codeElement) ? codeElement.GetString() : null;
                errorMessage = error.TryGetProperty("Message", out var messageElement) ? messageElement.GetString() : null;
            }
            else if (root.TryGetProperty("SendStatusSet", out var statusSet))
            {
                sendStatusSet = statusSet.Clone();
                hasSendStatusSet = true;
            }
        }
        catch (JsonException)
        {
            throw new BusinessException($"腾讯云短信返回无法解析：{SenderHttp.TruncateBody(responseBody)}") { StatusCode = 502 };
        }

        if (errorCode != null)
        {
            throw new BusinessException($"腾讯云短信发送失败：{errorCode} {errorMessage}") { StatusCode = 502 };
        }

        if (hasSendStatusSet)
        {
            var failed = sendStatusSet.EnumerateArray()
                .FirstOrDefault(x => !string.Equals(x.GetProperty("Code").GetString(), "Ok", StringComparison.Ordinal));
            if (failed.ValueKind == JsonValueKind.Object)
            {
                throw new BusinessException($"腾讯云短信发送失败：{failed.GetProperty("PhoneNumber").GetString()} {failed.GetProperty("Code").GetString()} {failed.GetProperty("Message").GetString()}") { StatusCode = 502 };
            }
        }
    }

    /// <summary>
    /// 计算 TC3-HMAC-SHA256 签名（Authorization 头）.
    /// </summary>
    /// <param name="config">渠道配置.</param>
    /// <param name="host">请求主机名.</param>
    /// <param name="action">接口名.</param>
    /// <param name="body">JSON 请求体.</param>
    /// <param name="timestamp">输出签名用 Unix 时间戳（秒）.</param>
    /// <returns>返回 Authorization 头值.</returns>
    private static string Sign(TencentSmsConfig config, string host, string action, string body, out long timestamp)
    {
        timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var date = DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var canonicalHeaders = $"content-type:application/json; charset=utf-8\nhost:{host}\nx-tc-action:{action.ToLowerInvariant()}\n";
        var signedHeaders = "content-type;host;x-tc-action";
        var hashedPayload = HexSha256(Encoding.UTF8.GetBytes(body));
        var canonicalRequest = $"POST\n/\n\n{canonicalHeaders}\n{signedHeaders}\n{hashedPayload}";

        var credentialScope = $"{date}/{Service}/tc3_request";
        var stringToSign = $"TC3-HMAC-SHA256\n{timestamp}\n{credentialScope}\n{HexSha256(Encoding.UTF8.GetBytes(canonicalRequest))}";

        var secretDate = HmacSha256(Encoding.UTF8.GetBytes($"TC3{config.SecretKey}"), Encoding.UTF8.GetBytes(date));
        var secretService = HmacSha256(secretDate, Encoding.UTF8.GetBytes(Service));
        var secretSigning = HmacSha256(secretService, Encoding.UTF8.GetBytes("tc3_request"));
        var signature = Convert.ToHexString(HmacSha256(secretSigning, Encoding.UTF8.GetBytes(stringToSign))).ToLowerInvariant();

        return $"TC3-HMAC-SHA256 Credential={config.SecretId}/{credentialScope}, SignedHeaders={signedHeaders}, Signature={signature}";
    }

    private static byte[] HmacSha256(byte[] key, byte[] data)
    {
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(data);
    }

    private static string HexSha256(byte[] data)
        => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
}

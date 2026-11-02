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
/// 阿里云短信发送器（Dysmsapi SendSms）：平台侧已审核模板 Code + 参数发送，收件人取消息动态手机号列表.
/// <para>签名按阿里云 POP RPC 签名 V1（HMAC-SHA1）实现；短信渠道必须使用通知模板（TemplateId）触发，
/// 模板未配置阿里云模板 Code 时发送失败并单独记录，不阻塞其它渠道.</para>
/// </summary>
public class AliyunSmsSender : INotificationChannelSender
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AliyunSmsSender"/> class.
    /// </summary>
    /// <param name="httpClientFactory">HTTP 客户端工厂.</param>
    public AliyunSmsSender(IHttpClientFactory httpClientFactory)
    {
        HttpClientFactory = httpClientFactory;
    }

    private IHttpClientFactory HttpClientFactory { get; }

    /// <inheritdoc/>
    public NotificationChannelType Type => NotificationChannelType.AliyunSms;

    /// <inheritdoc/>
    public async Task SendAsync(NotificationChannelEntity channel, NotificationMessage message, CancellationToken cancellationToken)
    {
        var config = NotificationChannelConfigs.Deserialize<AliyunSmsConfig>(channel.Config);
        var templateCode = message.SmsAliyunTemplateCode;
        if (string.IsNullOrWhiteSpace(templateCode))
        {
            throw new BusinessException("阿里云短信渠道必须使用已配置阿里云模板 Code 的通知模板发送") { StatusCode = 400 };
        }

        var phones = message.PhoneNumbers?
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .Distinct()
            .ToList();
        if (phones == null || phones.Count == 0)
        {
            throw new BusinessException("阿里云短信渠道投递失败：未指定收件人手机号（收件人由发送时动态提供）") { StatusCode = 400 };
        }

        // 公共参数 + 业务参数（RPC 风格，签名外附于查询串）
        var parameters = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["AccessKeyId"] = config.AccessKeyId,
            ["Action"] = "SendSms",
            ["Format"] = "JSON",
            ["PhoneNumbers"] = string.Join(",", phones),
            ["RegionId"] = "cn-hangzhou",
            ["SignName"] = config.SignName,
            ["SignatureMethod"] = "HMAC-SHA1",
            ["SignatureNonce"] = Guid.NewGuid().ToString(),
            ["SignatureVersion"] = "1.0",
            ["TemplateCode"] = templateCode,
            ["Timestamp"] = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
            ["Version"] = "2017-05-25"
        };

        if (message.TemplateVariables is { Count: > 0 })
        {
            // TemplateParam 为 JSON 对象，键名与平台模板变量一致（即 MoAI 模板定义的变量名）
            parameters["TemplateParam"] = JsonSerializer.Serialize(message.TemplateVariables, NotificationChannelConfigs.JsonOptions);
        }

        var canonicalQuery = string.Join("&", parameters.Select(x => $"{PopEncode(x.Key)}={PopEncode(x.Value)}"));
        var stringToSign = $"GET&{PopEncode("/")}&{PopEncode(canonicalQuery)}";
        using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(config.AccessKeySecret + "&"));
        var signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign)));

        var url = $"{config.Endpoint.TrimEnd('/')}?{canonicalQuery}&Signature={PopEncode(signature)}";
        var (statusCode, body) = await SenderHttp.GetAsync(HttpClientFactory, url, cancellationToken);
        SenderHttp.EnsureSuccessStatus(statusCode, body, "阿里云短信");

        string? code = null;
        string? errorMessage = null;
        try
        {
            using var doc = JsonDocument.Parse(body);
            code = doc.RootElement.TryGetProperty("Code", out var codeElement) ? codeElement.GetString() : null;
            errorMessage = doc.RootElement.TryGetProperty("Message", out var messageElement) ? messageElement.GetString() : null;
        }
        catch (JsonException)
        {
            throw new BusinessException($"阿里云短信返回无法解析：{SenderHttp.TruncateBody(body)}") { StatusCode = 502 };
        }

        if (!string.Equals(code, "OK", StringComparison.Ordinal))
        {
            throw new BusinessException(code == null
                ? $"阿里云短信返回无法解析：{SenderHttp.TruncateBody(body)}"
                : $"阿里云短信发送失败：{code} {errorMessage}") { StatusCode = 502 };
        }
    }

    /// <summary>
    /// 阿里云 POP 特殊 URL 编码：RFC3986，空格转 %20.
    /// </summary>
    /// <param name="value">原文.</param>
    /// <returns>返回编码后的文本.</returns>
    private static string PopEncode(string value)
        => Uri.EscapeDataString(value).Replace("+", "%20", StringComparison.Ordinal);
}

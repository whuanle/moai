using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MoAI.AIChannel.Models;
using MoAI.Infra.Exceptions;

namespace MoAI.AIChannel.Services;

/// <summary>
/// 依据协议调用供应商模型列表接口，获取当前账号可用的模型 id.
/// </summary>
public class AIModelProviderService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AIModelProviderService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIModelProviderService"/> class.
    /// </summary>
    /// <param name="httpClient"></param>
    /// <param name="logger"></param>
    public AIModelProviderService(HttpClient httpClient, ILogger<AIModelProviderService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// 获取渠道账号可用的模型 id 列表.
    /// </summary>
    /// <param name="protocol">协议.</param>
    /// <param name="baseUrl">接入端点.</param>
    /// <param name="apiKey">密钥.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回模型 id 列表.</returns>
    public async Task<IReadOnlyList<string>> GetModelIdsAsync(AIProtocolFamily protocol, string baseUrl, string apiKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(apiKey))
        {
            throw new BusinessException("渠道未配置接入端点或密钥，无法同步模型.") { StatusCode = 400 };
        }

        baseUrl = baseUrl.TrimEnd('/');
        var url = BuildEndpoint(protocol, baseUrl, apiKey);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        ApplyAuth(protocol, request, apiKey);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is not BusinessException)
        {
            _logger.LogError(ex, "获取模型列表失败.{@BaseUrl}", baseUrl);
            throw new BusinessException("无法访问供应商接口，请检查端点或网络.") { StatusCode = 400 };
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("获取模型列表失败.{StatusCode}.{Body}", (int)response.StatusCode, Truncate(body));
                throw new BusinessException("供应商返回错误，请检查密钥或权限.") { StatusCode = (int)response.StatusCode };
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return ParseModelIds(protocol, content);
        }
    }

    private static string BuildEndpoint(AIProtocolFamily protocol, string baseUrl, string apiKey)
    {
        return protocol switch
        {
            AIProtocolFamily.OpenAIChatCompletions or AIProtocolFamily.OpenAIResponses => $"{baseUrl}/models",
            AIProtocolFamily.AnthropicMessages => $"{baseUrl}/v1/models",
            AIProtocolFamily.GoogleGemini => $"{baseUrl}/v1beta/models?key={Uri.EscapeDataString(apiKey)}",
            _ => throw new BusinessException("不支持的协议类型.") { StatusCode = 400 },
        };
    }

    private static void ApplyAuth(AIProtocolFamily protocol, HttpRequestMessage request, string apiKey)
    {
        switch (protocol)
        {
            case AIProtocolFamily.OpenAIChatCompletions:
            case AIProtocolFamily.OpenAIResponses:
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                break;
            case AIProtocolFamily.AnthropicMessages:
                request.Headers.TryAddWithoutValidation("x-api-key", apiKey);
                request.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");
                break;
            case AIProtocolFamily.GoogleGemini:
                // key 已拼入 query string
                break;
        }
    }

    private static IReadOnlyList<string> ParseModelIds(AIProtocolFamily protocol, string content)
    {
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;
        var ids = new List<string>();

        if (protocol == AIProtocolFamily.GoogleGemini)
        {
            if (root.TryGetProperty("models", out var models) && models.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in models.EnumerateArray())
                {
                    if (item.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String)
                    {
                        var modelId = name.GetString() ?? string.Empty;
                        var slash = modelId.LastIndexOf('/');
                        if (slash >= 0)
                        {
                            modelId = modelId[(slash + 1)..];
                        }

                        if (!string.IsNullOrWhiteSpace(modelId))
                        {
                            ids.Add(modelId);
                        }
                    }
                }
            }
        }
        else
        {
            if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in data.EnumerateArray())
                {
                    if (item.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(id.GetString()))
                    {
                        ids.Add(id.GetString()!);
                    }
                }
            }
        }

        return ids;
    }

    private static string Truncate(string value)
    {
        return value.Length <= 500 ? value : value[..500];
    }
}

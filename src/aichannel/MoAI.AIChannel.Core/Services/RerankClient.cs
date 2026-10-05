using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MoAI.AIChannel.Models;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;

namespace MoAI.AIChannel.Services;

/// <summary>
/// 重排序客户端实现：调用 OpenAI 兼容渠道的 <c>POST {baseUrl}/rerank</c> 端点（Cohere/Jina 风格），响应兼容顶层 results 与 data.results 两种包装.
/// </summary>
public class RerankClient : IRerankClient
{
    /// <summary>
    /// 单次重排序文档数上限（BoCha 等供应商的请求级约束）.
    /// </summary>
    public const int MaxDocuments = 50;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly ILogger<RerankClient> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RerankClient"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP 客户端.</param>
    /// <param name="logger">日志.</param>
    public RerankClient(HttpClient httpClient, ILogger<RerankClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<RerankResultItem>> RerankAsync(AiModelEntity model, AiChannelEntity channel, string query, IReadOnlyList<string> documents, int? topN = null, CancellationToken cancellationToken = default)
    {
        if (documents.Count == 0)
        {
            return [];
        }

        var protocol = (AIProtocolFamily)channel.ProtocolFamily;
        if (protocol is not (AIProtocolFamily.OpenAIChatCompletions or AIProtocolFamily.OpenAIResponses))
        {
            throw new BusinessException($"渠道协议 {protocol} 不支持重排序.") { StatusCode = 400 };
        }

        if (string.IsNullOrWhiteSpace(channel.BaseUrl))
        {
            throw new BusinessException("渠道未配置接入端点，无法调用重排序模型.") { StatusCode = 400 };
        }

        var url = channel.BaseUrl.TrimEnd('/') + "/rerank";
        var payload = new RerankHttpRequest
        {
            Model = model.ModelId,
            Query = query,
            Documents = documents.Take(MaxDocuments).ToList(),
            TopN = topN,
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", channel.ApiKey);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("调用重排序模型超时.{@BaseUrl}", channel.BaseUrl);
            throw new BusinessException("重排序模型请求超时.") { StatusCode = 504 };
        }
        catch (Exception ex) when (ex is not BusinessException)
        {
            _logger.LogError(ex, "调用重排序模型失败.{@BaseUrl}", channel.BaseUrl);
            throw new BusinessException("无法访问重排序模型接口，请检查渠道端点或网络.") { StatusCode = 502 };
        }

        using (response)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("重排序模型返回错误.{StatusCode}.{Body}", (int)response.StatusCode, Truncate(content));
                throw new BusinessException("重排序模型返回错误.") { StatusCode = (int)response.StatusCode };
            }

            try
            {
                return ParseResults(content);
            }
            catch (JsonException)
            {
                _logger.LogWarning("重排序模型响应格式错误.{Body}", Truncate(content));
                throw new BusinessException("重排序模型响应格式错误.") { StatusCode = 502 };
            }
        }
    }

    private static List<RerankResultItem> ParseResults(string content)
    {
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        if (!TryGetResultsElement(root, out var resultsElement))
        {
            return [];
        }

        var items = new List<RerankResultItem>();
        foreach (var item in resultsElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object ||
                !item.TryGetProperty("index", out var indexEl) ||
                indexEl.ValueKind != JsonValueKind.Number ||
                !indexEl.TryGetInt32(out var index))
            {
                continue;
            }

            double score = 0;
            if (item.TryGetProperty("relevance_score", out var scoreEl))
            {
                if (scoreEl.ValueKind == JsonValueKind.Number && scoreEl.TryGetDouble(out var number))
                {
                    score = number;
                }
                else if (scoreEl.ValueKind == JsonValueKind.String && double.TryParse(scoreEl.GetString(), out var parsed))
                {
                    score = parsed;
                }
            }

            items.Add(new RerankResultItem(index, score));
        }

        return items;
    }

    private static bool TryGetResultsElement(JsonElement root, out JsonElement results)
    {
        // 顶层 results（Cohere/Jina/vLLM 风格）或 data.results（BoCha 风格）
        if (root.TryGetProperty("results", out var direct) && direct.ValueKind == JsonValueKind.Array)
        {
            results = direct;
            return true;
        }

        if (root.TryGetProperty("data", out var data) &&
            data.ValueKind == JsonValueKind.Object &&
            data.TryGetProperty("results", out var wrapped) &&
            wrapped.ValueKind == JsonValueKind.Array)
        {
            results = wrapped;
            return true;
        }

        results = default;
        return false;
    }

    private static string Truncate(string value)
    {
        return value.Length <= 500 ? value : value[..500];
    }

    private sealed class RerankHttpRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; init; } = string.Empty;

        [JsonPropertyName("query")]
        public string Query { get; init; } = string.Empty;

        [JsonPropertyName("documents")]
        public List<string> Documents { get; init; } = new();

        [JsonPropertyName("top_n")]
        public int? TopN { get; init; }
    }
}

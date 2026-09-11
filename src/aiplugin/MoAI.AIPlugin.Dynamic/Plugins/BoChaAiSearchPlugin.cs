using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MoAI.AIPlugin.Attributes;
using MoAI.AIPlugin.Contracts;
using MoAI.AIPlugin.Dynamic.Models;
using MoAI.Infra.BoCha;
using MoAI.Infra.BoCha.Models;
using MoAI.Infra.Exceptions;
using Refit;

namespace MoAI.AIPlugin.Dynamic.Plugins;

/// <summary>
/// 博查 AI 搜索（动态插件）：调用博查 AI Search API，一次返回总结答案、追问问题、参考网页与图片，以及天气、百科、股票等垂域模态卡.
/// </summary>
[AiPlugin(key: "bocha_ai_search", Name = "博查 AI 搜索", Description = "调用博查 AI Search API 检索全网，返回大模型总结答案、追问问题、参考网页/图片与天气、百科、股票等垂域模态卡")]
public class BoChaAiSearchPlugin : IDynamicPluginRuntime<BoChaAiSearchRequest, BoChaAiSearchResponse, BoChaAiSearchConfig>
{
    private const int MinCount = 1;
    private const int MaxCount = 50;
    private const string NoLimit = "noLimit";

    private const string MessageTypeSource = "source";
    private const string MessageTypeAnswer = "answer";
    private const string MessageTypeFollowUp = "follow_up";
    private const string ContentTypeWebPage = "webpage";
    private const string ContentTypeImage = "image";

    private readonly IBoChaClient _client;
    private BoChaAiSearchConfig _config = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="BoChaAiSearchPlugin"/> class.
    /// </summary>
    /// <param name="client">博查 API 客户端（由基础设施层注册，复用统一的外部请求日志与遥测）.</param>
    public BoChaAiSearchPlugin(IBoChaClient client)
    {
        _client = client;
    }

    /// <inheritdoc/>
    public static string GetParamsExampleValue()
    {
        return """
            {
              "Query": "西瓜的功效与作用",  // 搜索词，可为关键词或自然语言问题
              "Freshness": "noLimit",        // noLimit | oneDay | oneWeek | oneMonth | oneYear
              "Include": null,               // 仅在这些站点内搜索，如 qq.com|m.163.com
              "Count": 10,                   // 参考网页条数 1-50
              "Answer": true                 // 是否由大模型生成总结答案与追问问题
            }
            """;
    }

    /// <inheritdoc/>
    public static string GetConfigExampleValue()
    {
        return """
            {
              "ApiKey": "sk-xxxxxxxx" // 博查开放平台 API Key，前往 https://open.bocha.cn 的「API KEY 管理」获取
            }
            """;
    }

    /// <inheritdoc/>
    public Task<string?> InitAsync(BoChaAiSearchConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.ApiKey))
        {
            return Task.FromResult<string?>("API Key 不能为空，请先在 https://open.bocha.cn 获取");
        }

        _config = config;
        return Task.FromResult<string?>(null);
    }

    /// <inheritdoc/>
    public async Task<BoChaAiSearchResponse> RunAsync(BoChaAiSearchRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            throw new BusinessException(400, "搜索词 Query 不能为空");
        }

        var query = request.Query.Trim();
        var searchRequest = new AiSearchRequest
        {
            Query = query,
            Freshness = string.IsNullOrWhiteSpace(request.Freshness) ? NoLimit : request.Freshness.Trim(),
            Count = Math.Clamp(request.Count, MinCount, MaxCount),

            // 插件引擎是「一问一答」模型，无法把 SSE 增量透出给调用方，因此固定走非流式响应；
            // Answer=true 时博查会先由大模型生成完整答案再一次性返回，等价拿到同样内容。
            Answer = request.Answer,
            Stream = false,
            Include = request.Include,
        };

        AiSearchResponse response;
        try
        {
            response = await _client.AiSearchAsync(BoChaAuthorization.Build(_config.ApiKey), searchRequest)
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (ApiException ex)
        {
            // Refit 对非 2xx 直接抛 ApiException，这里换成带响应内容的业务异常，
            // 便于在运行抽屉中定位（401 密钥无效、403 余额不足、429 限流等）。
            throw new BusinessException((int)ex.StatusCode, $"博查 AI Search 调用失败（HTTP {(int)ex.StatusCode}）：{ex.Content ?? ex.ReasonPhrase}");
        }

        // HTTP 200 但响应体 code 非 200 的兜底处理.
        _client.HandleApiError(response);
        return Map(response, query);
    }

    private static BoChaAiSearchResponse Map(AiSearchResponse response, string query)
    {
        var webPages = new List<BoChaWebPage>();
        var images = new List<BoChaWebImage>();
        var modelCards = new List<BoChaAiModelCard>();
        var followUps = new List<string>();
        var answer = new StringBuilder();
        var someResultsRemoved = false;

        foreach (var message in response.Messages ?? [])
        {
            if (string.IsNullOrWhiteSpace(message.Content))
            {
                continue;
            }

            if (!string.Equals(message.Type, MessageTypeSource, StringComparison.OrdinalIgnoreCase))
            {
                // type=answer 的 content 是 Markdown 正文；type=follow_up 的 content 是一个追问问题.
                if (string.Equals(message.Type, MessageTypeAnswer, StringComparison.OrdinalIgnoreCase))
                {
                    answer.Append(message.Content);
                }
                else if (string.Equals(message.Type, MessageTypeFollowUp, StringComparison.OrdinalIgnoreCase))
                {
                    followUps.Add(message.Content);
                }

                continue;
            }

            // type=source 时 content 是 JSON 文本：网页/图片为 {"value":[...]} 包装，模态卡为数组或对象.
            var document = TryParseContent(message.Content);
            if (document == null)
            {
                continue;
            }

            using (document)
            {
                var contentType = message.ContentType ?? string.Empty;
                someResultsRemoved |= ReadBool(document.RootElement, "someResultsRemoved") ?? false;

                foreach (var item in EnumerateItems(document.RootElement))
                {
                    if (string.Equals(contentType, ContentTypeWebPage, StringComparison.OrdinalIgnoreCase))
                    {
                        var page = MapWebPage(item);
                        if (page != null)
                        {
                            webPages.Add(page);
                        }
                    }
                    else if (string.Equals(contentType, ContentTypeImage, StringComparison.OrdinalIgnoreCase))
                    {
                        var image = MapImage(item);
                        if (image != null)
                        {
                            images.Add(image);
                        }
                    }
                    else
                    {
                        // 其余 content_type 均为模态卡（weather_china_v2、baike_pro_v2、douyin 等）.
                        var card = MapModelCard(item, contentType);
                        if (card != null)
                        {
                            modelCards.Add(card);
                        }
                    }
                }
            }
        }

        return new BoChaAiSearchResponse
        {
            Query = query,
            ConversationId = response.ConversationId,
            Answer = answer.Length == 0 ? null : answer.ToString(),
            FollowUps = followUps,
            WebPages = webPages,
            Images = images,
            ModelCards = modelCards,
            SomeResultsRemoved = someResultsRemoved,
        };
    }

    /// <summary>
    /// 展开参考源条目：兼容非流式的 <c>{"value":[...]}</c> 包装、流式的单条对象与裸数组三种形态.
    /// </summary>
    /// <param name="root">content 的 JSON 根元素.</param>
    /// <returns>逐条参考源.</returns>
    private static IEnumerable<JsonElement> EnumerateItems(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
            {
                yield return item;
            }

            yield break;
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            yield break;
        }

        if (root.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray())
            {
                yield return item;
            }

            yield break;
        }

        // 未开放的结果（如 video）会返回空对象 {}，此时不产出条目.
        if (root.EnumerateObject().Any())
        {
            yield return root;
        }
    }

    private static BoChaWebPage? MapWebPage(JsonElement item)
    {
        var name = ReadString(item, "name");
        var url = ReadString(item, "url");
        if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(url))
        {
            return null;
        }

        return new BoChaWebPage
        {
            Name = name ?? string.Empty,
            Url = url ?? string.Empty,
            Snippet = ReadString(item, "snippet") ?? string.Empty,
            Summary = ReadString(item, "summary"),
            SiteName = ReadString(item, "siteName"),
            SiteIcon = ReadString(item, "siteIcon"),
            DatePublished = ReadString(item, "datePublished"),
        };
    }

    private static BoChaWebImage? MapImage(JsonElement item)
    {
        var thumbnailUrl = ReadString(item, "thumbnailUrl");
        var contentUrl = ReadString(item, "contentUrl");
        if (string.IsNullOrEmpty(thumbnailUrl) && string.IsNullOrEmpty(contentUrl))
        {
            return null;
        }

        return new BoChaWebImage
        {
            Name = ReadString(item, "name"),
            ThumbnailUrl = thumbnailUrl,
            ContentUrl = contentUrl,
            HostPageUrl = ReadString(item, "hostPageUrl"),
            Width = ReadInt(item, "width"),
            Height = ReadInt(item, "height"),
        };
    }

    private static BoChaAiModelCard? MapModelCard(JsonElement item, string contentType)
    {
        if (item.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var card = new BoChaAiModelCard
        {
            Type = contentType,
            Name = ReadString(item, "name"),
            Url = ReadString(item, "url"),
            Snippet = ReadString(item, "snippet") ?? ReadString(item, "description"),
            Summary = ReadString(item, "summary"),
            SiteName = ReadString(item, "siteName"),
            SiteIcon = ReadString(item, "siteIcon"),
            DatePublished = ReadString(item, "datePublished"),
        };

        // 通用字段一个都取不到时说明该结构不认识（例如空对象），直接丢弃，避免产出全空条目.
        if (card.Name == null && card.Url == null && card.Snippet == null && card.Summary == null)
        {
            return null;
        }

        return card;
    }

    private static JsonDocument? TryParseContent(string content)
    {
        try
        {
            return JsonDocument.Parse(content);
        }
        catch (JsonException)
        {
            // 内容不是 JSON 时不阻塞其它消息的解析.
            return null;
        }
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => property.ToString(),
            _ => null,
        };
    }

    private static int? ReadInt(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var value))
        {
            return value;
        }

        if (property.ValueKind == JsonValueKind.String && int.TryParse(property.GetString(), out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static bool? ReadBool(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };
    }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MoAI.AIChannel.Models;
using MoAI.Infra;

namespace MoAI.AIChannel.Services;

/// <summary>
/// MoPlatform 模型目录（models.json）加载与查询服务.
/// 读取 configs/models.json，并按文件时间戳自动重载，无需重启.
/// </summary>
public class AIModelCatalogService
{
    private static readonly string ConfigsCatalogPath = Path.Combine(AppConst.ConfigsPath, "models.json");

    private readonly ILogger<AIModelCatalogService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Dictionary<string, Dictionary<string, AIChannelModelMeta>>? _cache;
    private (string Path, DateTime WriteTimeUtc, long Length)? _cacheStamp;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIModelCatalogService"/> class.
    /// </summary>
    /// <param name="logger"></param>
    public AIModelCatalogService(ILogger<AIModelCatalogService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 失效内存缓存，下次查询时重新加载目录文件.
    /// </summary>
    public void InvalidateCache()
    {
        _cache = null;
        _cacheStamp = null;
    }

    /// <summary>
    /// 按 modelId 查询目录中的模型元数据，不存在返回 null.
    /// 匹配优先级：指定 provider 精确 → 全目录精确（忽略大小写）→ 全目录按最后一段匹配.
    /// </summary>
    /// <param name="providerKey">渠道标识（仅为优先匹配用）.</param>
    /// <param name="modelId">模型 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回 <see cref="AIChannelModelMeta"/>.</returns>
    public async Task<AIChannelModelMeta?> GetModelAsync(string providerKey, string modelId, CancellationToken cancellationToken = default)
    {
        var catalog = await EnsureLoadedAsync(cancellationToken);

        // 1) 指定 provider 精确匹配
        if (catalog.TryGetValue(providerKey, out var providerModels) && TryGetExact(providerModels, modelId, out var exact))
        {
            return exact;
        }

        // 2) 全目录精确匹配（忽略大小写）
        foreach (var models in catalog.Values)
        {
            if (TryGetExact(models, modelId, out var exactGlobal))
            {
                return exactGlobal;
            }
        }

        // 3) 全目录按最后一段匹配（例如 deepseek-v3 匹配 deepseek-ai/DeepSeek-V3）
        var lastSegment = LastSegment(modelId);
        foreach (var models in catalog.Values)
        {
            foreach (var (key, meta) in models)
            {
                if (string.Equals(LastSegment(key), lastSegment, StringComparison.OrdinalIgnoreCase))
                {
                    return meta;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// 解析 models.json 原文为目录结构，格式非法抛 <see cref="JsonException"/>.
    /// </summary>
    /// <param name="raw">models.json 原文.</param>
    /// <returns>provider id → 模型元数据字典.</returns>
    public static Dictionary<string, Dictionary<string, AIChannelModelMeta>> ParseCatalog(string raw)
    {
        using var doc = JsonDocument.Parse(raw);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            // 统一抛 JsonException，调用方按格式非法处理
            throw new JsonException("models.json 根节点必须是对象.");
        }

        if (root.TryGetProperty("schemaVersion", out _) || root.TryGetProperty("channels", out _))
        {
            var channelsElement = root.TryGetProperty("channels", out var channels) ? channels : default;
            return ParseMoPlatformCatalog(root, channelsElement);
        }

        var catalog = new Dictionary<string, Dictionary<string, AIChannelModelMeta>>(StringComparer.Ordinal);

        foreach (var provider in root.EnumerateObject())
        {
            if (!provider.Value.TryGetProperty("models", out var modelsElement) || modelsElement.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var providerModels = new Dictionary<string, AIChannelModelMeta>(StringComparer.Ordinal);
            foreach (var model in modelsElement.EnumerateObject())
            {
                var meta = ParseModel(model.Name, model.Value);
                if (meta != null)
                {
                    providerModels[meta.ModelId] = meta;
                }
            }

            catalog[provider.Name] = providerModels;
        }

        return catalog;
    }

    private static Dictionary<string, Dictionary<string, AIChannelModelMeta>> ParseMoPlatformCatalog(JsonElement root, JsonElement channelsElement)
    {
        if (!root.TryGetProperty("schemaVersion", out var schemaVersion)
            || schemaVersion.ValueKind != JsonValueKind.Number
            || !schemaVersion.TryGetInt32(out var version)
            || version != 1
            || channelsElement.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("MoPlatform models.json schemaVersion 必须为 1，且 channels 必须是数组.");
        }

        var catalog = new Dictionary<string, Dictionary<string, AIChannelModelMeta>>(StringComparer.Ordinal);
        foreach (var channel in channelsElement.EnumerateArray())
        {
            if (channel.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var providerKey = GetString(channel, "code");
            if (string.IsNullOrWhiteSpace(providerKey))
            {
                continue;
            }

            var providerModels = new Dictionary<string, AIChannelModelMeta>(StringComparer.Ordinal);
            if (channel.TryGetProperty("models", out var modelsElement) && modelsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var model in modelsElement.EnumerateArray())
                {
                    var meta = ParseMoPlatformModel(model);
                    if (meta != null)
                    {
                        providerModels[meta.ModelId] = meta;
                    }
                }
            }

            catalog[providerKey] = providerModels;
        }

        return catalog;
    }

    private static AIChannelModelMeta? ParseMoPlatformModel(JsonElement model)
    {
        if (model.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var modelId = GetString(model, "id");
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return null;
        }

        var limit = GetObject(model, "limit");
        var pricing = GetObject(model, "pricing");
        var inputModalities = GetStringArray(GetArray(model, "inputModalities"));
        var outputModalities = GetStringArray(GetArray(model, "outputModalities"));
        var capabilities = GetStringArray(GetArray(model, "capabilities"));

        return new AIChannelModelMeta
        {
            ModelId = modelId,
            Name = GetString(model, "name") ?? modelId,
            Description = GetString(model, "description"),
            Family = GetString(model, "family"),
            ModelKind = MapMoPlatformModelKind(GetString(model, "kind")),
            SupportsVision = inputModalities?.Contains("image", StringComparer.OrdinalIgnoreCase) == true,
            SupportsAttachments = HasCapability(capabilities, "attachment"),
            SupportsReasoning = HasCapability(capabilities, "reasoning"),
            SupportsToolCall = HasCapability(capabilities, "toolCall"),
            SupportsStructuredOutput = HasCapability(capabilities, "structuredOutput"),
            SupportsTemperature = HasCapability(capabilities, "temperature"),
            KnowledgeCutoff = GetString(model, "knowledgeCutoff"),
            ReleaseDate = GetString(model, "releaseDate"),
            InputModalities = inputModalities,
            OutputModalities = outputModalities,
            OpenWeights = GetBool(model, "openWeights"),
            ContextWindow = GetInt(limit, "context"),
            MaxOutput = GetInt(limit, "output"),
            CostInput = GetDecimal(pricing, "input"),
            CostOutput = GetDecimal(pricing, "output"),
            CostCacheRead = GetDecimal(pricing, "cacheRead"),
        };
    }

    private static string? MapMoPlatformModelKind(string? kind)
    {
        return kind switch
        {
            "chat" or "realtime" or "moderation" or "ocr" => "conversation",
            "embedding" => "embedding",
            "rerank" => "rerank",
            "image" => "image-generation",
            "video" => "video-generation",
            "tts" or "asr" => "transcription",
            _ => null,
        };
    }

    private static bool HasCapability(List<string>? capabilities, string expected)
    {
        return capabilities?.Contains(expected, StringComparer.OrdinalIgnoreCase) == true;
    }

    private static bool TryGetExact(Dictionary<string, AIChannelModelMeta> models, string modelId, out AIChannelModelMeta? meta)
    {
        foreach (var (key, value) in models)
        {
            if (string.Equals(key, modelId, StringComparison.OrdinalIgnoreCase))
            {
                meta = value;
                return true;
            }
        }

        meta = null;
        return false;
    }

    private static string LastSegment(string modelId)
    {
        var slash = modelId.LastIndexOf('/');
        return slash >= 0 ? modelId[(slash + 1)..] : modelId;
    }

    private async Task<Dictionary<string, Dictionary<string, AIChannelModelMeta>>> EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        var stamp = CurrentStamp();
        if (_cache != null && _cacheStamp == stamp)
        {
            return _cache;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_cache != null && _cacheStamp == stamp)
            {
                return _cache;
            }

            _cache = Load();
            _cacheStamp = CurrentStamp();
            return _cache;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static string? ResolveCatalogPath()
    {
        if (File.Exists(ConfigsCatalogPath))
        {
            return ConfigsCatalogPath;
        }

        return null;
    }

    private static (string Path, DateTime WriteTimeUtc, long Length)? CurrentStamp()
    {
        var path = ResolveCatalogPath();
        if (path == null)
        {
            return null;
        }

        var info = new FileInfo(path);
        return (path, info.LastWriteTimeUtc, info.Length);
    }

    private Dictionary<string, Dictionary<string, AIChannelModelMeta>> Load()
    {
        var path = ResolveCatalogPath();
        if (path == null)
        {
            _logger.LogWarning("模型目录文件不存在：{Configs}", ConfigsCatalogPath);
            return new Dictionary<string, Dictionary<string, AIChannelModelMeta>>();
        }

        try
        {
            return ParseCatalog(File.ReadAllText(path));
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "模型目录解析失败，当前目录按空目录处理：{Path}", path);
            return new Dictionary<string, Dictionary<string, AIChannelModelMeta>>();
        }
    }

    private static AIChannelModelMeta? ParseModel(string modelId, JsonElement model)
    {
        if (model.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var name = GetString(model, "name") ?? modelId;
        var limit = GetObject(model, "limit");
        var cost = GetObject(model, "cost");
        var modalities = GetObject(model, "modalities");

        return new AIChannelModelMeta
        {
            ModelId = modelId,
            Name = name,
            Description = GetString(model, "description"),
            Family = GetString(model, "family"),
            SupportsAttachments = GetBool(model, "attachment"),
            SupportsReasoning = GetBool(model, "reasoning"),
            SupportsToolCall = GetBool(model, "tool_call"),
            SupportsStructuredOutput = GetBool(model, "structured_output"),
            SupportsTemperature = GetBool(model, "temperature"),
            KnowledgeCutoff = GetString(model, "knowledge"),
            ReleaseDate = GetString(model, "release_date"),
            LastUpdated = GetString(model, "last_updated"),
            InputModalities = GetStringArray(modalities, "input"),
            OutputModalities = GetStringArray(modalities, "output"),
            OpenWeights = GetBool(model, "open_weights"),
            ContextWindow = GetInt(limit, "context"),
            MaxOutput = GetInt(limit, "output"),
            CostInput = GetDecimal(cost, "input"),
            CostOutput = GetDecimal(cost, "output"),
            CostCacheRead = GetDecimal(cost, "cache_read"),
        };
    }

    private static string? GetString(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static bool GetBool(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.True;
    }

    private static JsonElement? GetObject(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Object
            ? value
            : (JsonElement?)null;
    }

    private static JsonElement? GetArray(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Array
            ? value
            : (JsonElement?)null;
    }

    private static int? GetInt(JsonElement? element, string property)
    {
        if (element is not { } e || !e.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        return value.TryGetInt32(out var i) ? i : null;
    }

    private static decimal? GetDecimal(JsonElement? element, string property)
    {
        if (element is not { } e || !e.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        return value.TryGetDecimal(out var d) ? d : null;
    }

    private static List<string>? GetStringArray(JsonElement? element, string property)
    {
        if (element is not { } e || !e.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var list = new List<string>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
            {
                list.Add(item.GetString()!);
            }
        }

        return list.Count > 0 ? list : null;
    }

    private static List<string>? GetStringArray(JsonElement? element)
    {
        if (element is not { ValueKind: JsonValueKind.Array } array)
        {
            return null;
        }

        var list = new List<string>();
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
            {
                list.Add(item.GetString()!);
            }
        }

        return list.Count > 0 ? list : null;
    }
}

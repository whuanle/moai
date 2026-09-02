using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MoAI.AIChannel.Models;
using MoAI.Infra.Exceptions;

namespace MoAI.AIChannel.Services;

/// <summary>
/// 后端内置模型目录（models.json）加载与查询服务.
/// </summary>
public class AIModelCatalogService
{
    private static readonly string CatalogPath = Path.Combine(AppContext.BaseDirectory, "models.json");

    private readonly ILogger<AIModelCatalogService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Dictionary<string, Dictionary<string, AIChannelModelMeta>>? _cache;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIModelCatalogService"/> class.
    /// </summary>
    /// <param name="logger"></param>
    public AIModelCatalogService(ILogger<AIModelCatalogService> logger)
    {
        _logger = logger;
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
        if (_cache != null)
        {
            return _cache;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_cache != null)
            {
                return _cache;
            }

            _cache = Load();
            return _cache;
        }
        finally
        {
            _gate.Release();
        }
    }

    private Dictionary<string, Dictionary<string, AIChannelModelMeta>> Load()
    {
        if (!File.Exists(CatalogPath))
        {
            _logger.LogWarning("模型目录文件不存在：{Path}", CatalogPath);
            return new Dictionary<string, Dictionary<string, AIChannelModelMeta>>();
        }

        try
        {
            var raw = File.ReadAllText(CatalogPath);
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
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
        catch (JsonException ex)
        {
            _logger.LogError(ex, "模型目录解析失败：{Path}", CatalogPath);
            throw new BusinessException("模型目录 models.json 解析失败.") { StatusCode = 500 };
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
}

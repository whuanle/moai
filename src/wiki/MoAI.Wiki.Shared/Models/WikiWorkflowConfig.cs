using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MoAI.Wiki.Models;

/// <summary>
/// 知识库默认工作流配置：切割 / 元数据生成 / 向量化三步预设.
/// 由 <see cref="Commands.UpdateWikiWorkflowCommand"/> 整体保存，批量执行工作流时作为前端预填默认值.
/// </summary>
public class WikiWorkflowConfig
{
    /// <summary>
    /// 文档切割预设，为空表示未配置该步骤.
    /// </summary>
    [JsonPropertyName("partition")]
    public WikiWorkflowPartitionOptions? Partition { get; set; }

    /// <summary>
    /// 元数据生成预设，为空表示未配置该步骤.
    /// </summary>
    [JsonPropertyName("metadata")]
    public WikiWorkflowMetadataOptions? Metadata { get; set; }

    /// <summary>
    /// 向量化预设，为空表示未配置该步骤.
    /// </summary>
    [JsonPropertyName("embedding")]
    public WikiWorkflowEmbeddingOptions? Embedding { get; set; }
}

/// <summary>
/// 工作流切割模式：普通切割或 AI 智能切割.
/// </summary>
public enum WorkflowPartitionMode
{
    /// <summary>
    /// 普通切割（Maomi.ToMarkdown TextSplit 规则切分）.
    /// </summary>
    [JsonPropertyName("normal")]
    Normal = 0,

    /// <summary>
    /// AI 智能切割（对话模型按语义输出 JSON 字符串数组）.
    /// </summary>
    [JsonPropertyName("ai")]
    Ai = 1,
}

/// <summary>
/// 默认工作流·文档切割预设.
/// </summary>
public class WikiWorkflowPartitionOptions
{
    /// <summary>
    /// 切割模式；缺省为普通切割（兼容历史 JSON）.
    /// </summary>
    [JsonPropertyName("mode")]
    public WorkflowPartitionMode Mode { get; set; } = WorkflowPartitionMode.Normal;

    /// <summary>
    /// 切割模式为普通切割时的切割方式.
    /// </summary>
    [JsonPropertyName("splitMode")]
    public DocumentPartitionSplitMode SplitMode { get; set; } = DocumentPartitionSplitMode.Markdown;

    /// <summary>
    /// 切片大小（1-8192，单位由 <see cref="SizeUnit"/> 决定），仅普通切割.
    /// </summary>
    [JsonPropertyName("chunkSize")]
    public int ChunkSize { get; set; }

    /// <summary>
    /// 切片重叠大小（0-8192，单位由 <see cref="OverlapUnit"/> 决定），仅普通切割.
    /// </summary>
    [JsonPropertyName("chunkOverlap")]
    public int ChunkOverlap { get; set; }

    /// <summary>
    /// 重叠单位.
    /// </summary>
    [JsonPropertyName("overlapUnit")]
    public DocumentPartitionOverlapUnit OverlapUnit { get; set; } = DocumentPartitionOverlapUnit.Character;

    /// <summary>
    /// 切片大小计量单位.
    /// </summary>
    [JsonPropertyName("sizeUnit")]
    public DocumentPartitionSizeUnit SizeUnit { get; set; } = DocumentPartitionSizeUnit.Character;

    /// <summary>
    /// Token 计量时使用的编码名或模型名，为空默认 cl100k_base.
    /// </summary>
    [JsonPropertyName("tokenEncodingOrModel")]
    public string? TokenEncodingOrModel { get; set; }

    /// <summary>
    /// 切割模式为 AI 切割时使用的对话模型 id.
    /// </summary>
    [JsonPropertyName("aiModelId")]
    public Guid AiModelId { get; set; }

    /// <summary>
    /// AI 切割提示词模板，为空使用内置默认；仅 AI 切割.
    /// </summary>
    [JsonPropertyName("promptTemplate")]
    public string? PromptTemplate { get; set; }
}

/// <summary>
/// 默认工作流·元数据生成预设.
/// </summary>
public class WikiWorkflowMetadataOptions
{
    /// <summary>
    /// 元数据生成使用的对话模型 id.
    /// </summary>
    [JsonPropertyName("metadataModelId")]
    public Guid MetadataModelId { get; set; }

    /// <summary>
    /// 元数据生成策略（可多选）；为空或 null 时生成全套元数据.
    /// </summary>
    [JsonPropertyName("strategyTypes")]
    public List<MetadataGenerationStrategy>? StrategyTypes { get; set; }
}

/// <summary>
/// 默认工作流·向量化预设.
/// </summary>
public class WikiWorkflowEmbeddingOptions
{
    /// <summary>
    /// 是否对原文切片内容向量化.
    /// </summary>
    [JsonPropertyName("embedSourceText")]
    public bool EmbedSourceText { get; set; } = true;

    /// <summary>
    /// 是否对生成的元数据向量化.
    /// </summary>
    [JsonPropertyName("embedMetadata")]
    public bool EmbedMetadata { get; set; } = true;
}

/// <summary>
/// 默认工作流配置 JSON 序列化助手： camelCase 属性 + camelCase 枚举字符串，与全局 API 序列化约定一致.
/// </summary>
public static class WikiWorkflowConfigJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: true) },
    };

    /// <summary>
    /// 序列化为存储用 JSON.
    /// </summary>
    /// <param name="config">工作流配置.</param>
    /// <returns>JSON 字符串.</returns>
    public static string Serialize(WikiWorkflowConfig config)
    {
        return JsonSerializer.Serialize(config, Options);
    }

    /// <summary>
    /// 从存储 JSON 反序列化；空串或非法 JSON 返回 null（视为未配置）.
    /// </summary>
    /// <param name="json">存储 JSON.</param>
    /// <returns>工作流配置，失败时为 null.</returns>
    public static WikiWorkflowConfig? Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<WikiWorkflowConfig>(json, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

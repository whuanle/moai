using MoAI.Plugin.Attributes;
using System.Text.Json.Serialization;

namespace MoAI.Plugin.Plugins.BailianRetrieve;

/// <summary>
/// 阿里云百炼知识库检索插件配置.
/// </summary>
public class BailianRetrievePluginConfig
{
    /// <summary>
    /// 阿里云 AccessKey ID.
    /// </summary>
    [JsonPropertyName("AccessKeyId")]
    [NativePluginConfigField(
        Key = nameof(AccessKeyId),
        Description = "阿里云 AccessKey ID",
        FieldType = PluginConfigFieldType.String,
        IsRequired = true,
        ExampleValue = "LTAI5txxxxxxxx")]
    public string AccessKeyId { get; set; } = string.Empty;

    /// <summary>
    /// 阿里云 AccessKey Secret.
    /// </summary>
    [JsonPropertyName("AccessKeySecret")]
    [NativePluginConfigField(
        Key = nameof(AccessKeySecret),
        Description = "阿里云 AccessKey Secret",
        FieldType = PluginConfigFieldType.String,
        IsRequired = true,
        ExampleValue = "xxxxxxxxxxxxxxxxxxxxxxxx")]
    public string AccessKeySecret { get; set; } = string.Empty;

    /// <summary>
    /// 工作空间 ID.
    /// </summary>
    [JsonPropertyName("WorkspaceId")]
    [NativePluginConfigField(
        Key = nameof(WorkspaceId),
        Description = "工作空间 ID",
        FieldType = PluginConfigFieldType.String,
        IsRequired = true,
        ExampleValue = "ws_3Nt27MYcoK191ISp")]
    public string WorkspaceId { get; set; } = string.Empty;

    /// <summary>
    /// 知识库主键 ID.
    /// </summary>
    [JsonPropertyName("IndexId")]
    [NativePluginConfigField(
        Key = nameof(IndexId),
        Description = "知识库主键 ID",
        FieldType = PluginConfigFieldType.String,
        IsRequired = true,
        ExampleValue = "5pwe0m2g6t")]
    public string IndexId { get; set; } = string.Empty;

    /// <summary>
    /// 向量检索 Top K，默认 100，范围 0-100.
    /// </summary>
    [JsonPropertyName("DenseSimilarityTopK")]
    [NativePluginConfigField(
        Key = nameof(DenseSimilarityTopK),
        Description = "向量检索 Top K (0-100)",
        FieldType = PluginConfigFieldType.Number,
        IsRequired = false,
        ExampleValue = "100")]
    public int? DenseSimilarityTopK { get; set; }

    /// <summary>
    /// 关键词检索 Top K，默认 100，范围 0-100.
    /// </summary>
    [JsonPropertyName("SparseSimilarityTopK")]
    [NativePluginConfigField(
        Key = nameof(SparseSimilarityTopK),
        Description = "关键词检索 Top K (0-100)",
        FieldType = PluginConfigFieldType.Number,
        IsRequired = false,
        ExampleValue = "100")]
    public int? SparseSimilarityTopK { get; set; }
}

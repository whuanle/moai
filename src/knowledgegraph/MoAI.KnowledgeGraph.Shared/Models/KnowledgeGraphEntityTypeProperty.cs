using System.Text.Json.Serialization;

namespace MoAI.KnowledgeGraph.Models;

/// <summary>
/// 实体类型属性定义（模型页配置，实例录入时按此渲染并存储到图库节点）.
/// </summary>
public sealed record KnowledgeGraphEntityTypeProperty
{
    /// <summary>
    /// 属性名.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 属性类型：string / number / boolean / date.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = TypeString;

    /// <summary>
    /// 是否必填.
    /// </summary>
    [JsonPropertyName("required")]
    public bool Required { get; init; }

    /// <summary>
    /// 属性说明.
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 文本类型.
    /// </summary>
    public const string TypeString = "string";

    /// <summary>
    /// 数字类型.
    /// </summary>
    public const string TypeNumber = "number";

    /// <summary>
    /// 布尔类型.
    /// </summary>
    public const string TypeBoolean = "boolean";

    /// <summary>
    /// 日期类型.
    /// </summary>
    public const string TypeDate = "date";

    /// <summary>
    /// 合法的属性类型集合.
    /// </summary>
    public static readonly IReadOnlyList<string> AllowedTypes = new[] { TypeString, TypeNumber, TypeBoolean, TypeDate };
}

using System.Text.Json;
using MoAI.KnowledgeGraph.Models;

namespace MoAI.KnowledgeGraph.Services;

/// <summary>
/// 实体类型属性定义 JSON 序列化助手（PG jsonb 列与图库节点 propsJson 共用，容错解析历史脏数据）.
/// </summary>
public static class KnowledgeGraphPropertyJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// 反序列化属性定义，异常或空值返回空列表.
    /// </summary>
    /// <param name="json">JSON 字符串.</param>
    /// <returns>属性定义列表.</returns>
    public static List<KnowledgeGraphEntityTypeProperty> ParseDefinitions(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<KnowledgeGraphEntityTypeProperty>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<KnowledgeGraphEntityTypeProperty>>(json, Options) ?? new List<KnowledgeGraphEntityTypeProperty>();
        }
        catch (JsonException)
        {
            return new List<KnowledgeGraphEntityTypeProperty>();
        }
    }

    /// <summary>
    /// 序列化属性定义.
    /// </summary>
    /// <param name="properties">属性定义列表.</param>
    /// <returns>JSON 字符串.</returns>
    public static string WriteDefinitions(IReadOnlyList<KnowledgeGraphEntityTypeProperty> properties)
        => JsonSerializer.Serialize(properties, Options);

    /// <summary>
    /// 反序列化节点属性值（图库 propsJson），异常或空值返回空字典.
    /// </summary>
    /// <param name="json">JSON 字符串.</param>
    /// <returns>属性值字典.</returns>
    public static Dictionary<string, string> ParseValues(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json, Options) ?? new Dictionary<string, string>(StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    /// <summary>
    /// 序列化节点属性值.
    /// </summary>
    /// <param name="values">属性值字典.</param>
    /// <returns>JSON 字符串.</returns>
    public static string WriteValues(IReadOnlyDictionary<string, string> values)
        => JsonSerializer.Serialize(values, Options);
}

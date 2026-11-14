using System.Text.Json;
using MoAI.Publication.Models;

namespace MoAI.Publication.Queries;

/// <summary>
/// 应用上架可视范围组织授权快照（JSON）序列化辅助：申请时序列化落库、回显与审批物化时反序列化.
/// </summary>
internal static class VisibleOrgGrantSnapshot
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// 序列化为 JSON 快照（空清单返回空串）.
    /// </summary>
    /// <param name="grants">组织授权清单.</param>
    /// <returns>JSON 文本.</returns>
    public static string Serialize(IReadOnlyList<VisibleOrgGrantItem> grants)
    {
        if (grants.Count == 0)
        {
            return string.Empty;
        }

        return JsonSerializer.Serialize(grants, Options);
    }

    /// <summary>
    /// 反序列化 JSON 快照（空/损坏文本返回空清单，快照为系统写入不提供损坏兜底外的容错）.
    /// </summary>
    /// <param name="json">JSON 文本.</param>
    /// <returns>组织授权清单.</returns>
    public static List<VisibleOrgGrantItem> Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<VisibleOrgGrantItem>>(json, Options) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}

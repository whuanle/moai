using MoAI.Infra.Models;

namespace MoAI.Classify.Queries.Responses;

/// <summary>
/// 分类项.
/// </summary>
public class ClassifyItem : AuditsInfo
{
    /// <summary>
    /// 分类 id.
    /// </summary>
    public int ClassifyId { get; init; }

    /// <summary>
    /// 分类类型：plugin|app|kb.
    /// </summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>
    /// 分类名称.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 分类描述.
    /// </summary>
    public string? Description { get; init; }
}

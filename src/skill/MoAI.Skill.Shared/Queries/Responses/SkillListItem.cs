using MoAI.Infra.Models;

namespace MoAI.Skill.Queries.Responses;

/// <summary>
/// 技能列表项.
/// </summary>
public class SkillListItem : AuditsInfo
{
    /// <summary>
    /// 技能 id.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// 技能标识.
    /// </summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>
    /// 技能名称.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 技能描述.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 是否系统内置技能.
    /// </summary>
    public bool IsSystem { get; init; }

    /// <summary>
    /// 是否禁用.
    /// </summary>
    public bool IsDisable { get; init; }

    /// <summary>
    /// 所属团队 id，0=系统内置或个人技能.
    /// </summary>
    public int TeamId { get; init; }

    /// <summary>
    /// 是否已上架市场公开.
    /// </summary>
    public bool IsPublic { get; init; }

    /// <summary>
    /// 待审核的上架申请 id，无待审核申请时为 null.
    /// </summary>
    public long? PendingPublicationId { get; init; }

    /// <summary>
    /// 技能包文件数量.
    /// </summary>
    public int FileCount { get; init; }
}

namespace MoAI.Skill.Queries.Responses;

/// <summary>
/// 可挂载技能选项.
/// </summary>
public class SkillOptionItem
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
    /// 所属团队 id，0=系统内置或个人技能.
    /// </summary>
    public int TeamId { get; init; }
}

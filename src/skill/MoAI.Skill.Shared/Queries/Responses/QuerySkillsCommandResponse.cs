namespace MoAI.Skill.Queries.Responses;

/// <summary>
/// 技能列表响应.
/// </summary>
public class QuerySkillsCommandResponse
{
    /// <summary>
    /// 总数量.
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// 技能列表.
    /// </summary>
    public IReadOnlyList<SkillListItem> Items { get; init; } = Array.Empty<SkillListItem>();
}

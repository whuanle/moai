namespace MoAI.Skill.Queries.Responses;

/// <summary>
/// 技能列表响应（市场/个人/团队列表共用）.
/// </summary>
public class QuerySkillListCommandResponse
{
    /// <summary>
    /// 技能列表.
    /// </summary>
    public IReadOnlyList<SkillListItem> Items { get; init; } = Array.Empty<SkillListItem>();
}

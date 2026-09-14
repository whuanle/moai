namespace MoAI.Skill.Queries.Responses;

/// <summary>
/// 可挂载技能选项列表响应.
/// </summary>
public class QuerySkillOptionsCommandResponse
{
    /// <summary>
    /// 技能选项列表.
    /// </summary>
    public IReadOnlyList<SkillOptionItem> Items { get; init; } = Array.Empty<SkillOptionItem>();
}

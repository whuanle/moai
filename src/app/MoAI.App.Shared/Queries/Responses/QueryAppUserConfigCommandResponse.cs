namespace MoAI.App.Queries.Responses;

/// <summary>
/// 用户级应用配置.
/// </summary>
public class QueryAppUserConfigCommandResponse
{
    /// <summary>
    /// 用户为新会话选择的专家提示词 id，0=未设置（使用应用默认提示词）.
    /// </summary>
    public int PromptId { get; init; }

    /// <summary>
    /// 用户自选技能 id 列表.
    /// </summary>
    public IReadOnlyList<Guid> Skills { get; init; } = Array.Empty<Guid>();

    /// <summary>
    /// 应用绑定技能 id 列表（应用所有者在应用配置中锁定，用户不可移除）.
    /// </summary>
    public IReadOnlyList<Guid> LockedSkills { get; init; } = Array.Empty<Guid>();
}

namespace MoAI.Skill.Services;

/// <summary>
/// 技能运行时信息，供 Agent 运行时装配工具使用.
/// </summary>
public class SkillRuntimeInfo
{
    /// <summary>
    /// 技能 id.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// 技能标识（蛇形）.
    /// </summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>
    /// 技能名称.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 技能描述，作为工具描述暴露给模型.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 使用说明（markdown），技能加载时注入给 Agent.
    /// </summary>
    public string Instructions { get; init; } = string.Empty;

    /// <summary>
    /// 是否系统内置技能.
    /// </summary>
    public bool IsSystem { get; init; }

    /// <summary>
    /// 技能包文件清单.
    /// </summary>
    public IReadOnlyList<SkillRuntimeFile> Files { get; init; } = Array.Empty<SkillRuntimeFile>();
}

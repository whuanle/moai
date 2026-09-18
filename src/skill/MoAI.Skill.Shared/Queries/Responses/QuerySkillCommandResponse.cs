using MoAI.Skill.Models;

namespace MoAI.Skill.Queries.Responses;

/// <summary>
/// 技能详情响应.
/// </summary>
public class QuerySkillCommandResponse
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
    /// 使用说明（markdown）.
    /// </summary>
    public string Instructions { get; init; } = string.Empty;

    /// <summary>
    /// 技能包文件清单.
    /// </summary>
    public IReadOnlyList<SkillFileItem> Files { get; init; } = Array.Empty<SkillFileItem>();

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
    /// 分类 id，0 表示未分类.
    /// </summary>
    public int ClassifyId { get; init; }

    /// <summary>
    /// 是否已上架市场公开.
    /// </summary>
    public bool IsPublic { get; init; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; init; }

    /// <summary>
    /// 更新时间.
    /// </summary>
    public DateTimeOffset UpdateTime { get; init; }
}

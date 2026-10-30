using MoAI.Database.Entities;
using MoAI.Database.Enums;

namespace MoAI.Subsystem.Models;

/// <summary>
/// 子系统机器面调用者上下文：中间件按 token 中的子系统 id 从数据库实时解析（停用/删除即时生效）.
/// </summary>
public class SubsystemCallerContext
{
    /// <summary>
    /// 子系统 id.
    /// </summary>
    public Guid SubsystemId { get; init; }

    /// <summary>
    /// 子系统标识（appid）.
    /// </summary>
    public string Key { get; init; } = default!;

    /// <summary>
    /// 子系统名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 状态.
    /// </summary>
    public SubsystemStatus Status { get; init; }

    /// <summary>
    /// 团队授权范围.
    /// </summary>
    public SubsystemTeamScope TeamScope { get; init; }

    /// <summary>
    /// 从实体构造.
    /// </summary>
    /// <param name="entity">子系统实体.</param>
    /// <returns>返回调用者上下文.</returns>
    public static SubsystemCallerContext From(SubsystemEntity entity) => new()
    {
        SubsystemId = entity.Id,
        Key = entity.Key,
        Name = entity.Name,
        Status = entity.Status,
        TeamScope = entity.TeamScope,
    };
}

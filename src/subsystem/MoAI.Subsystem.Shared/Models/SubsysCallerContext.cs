using MoAI.Database.Entities;
using MoAI.Database.Enums;

namespace MoAI.Subsystem.Models;

/// <summary>
/// 系统级接入机器面调用者上下文（/api/subsys/**）：中间件按凭据主体从数据库实时解析（停用/删除即时生效），
/// 子系统与系统级应用接入共用同一上下文，以 <see cref="AppType"/> 区分.
/// </summary>
public class SubsysCallerContext
{
    /// <summary>
    /// 接入 id.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// 接入标识（appid）.
    /// </summary>
    public string Key { get; init; } = default!;

    /// <summary>
    /// 接入名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 系统级接入类型：subsystem 子系统 / app 应用接入.
    /// </summary>
    public AccessAppType AppType { get; init; }

    /// <summary>
    /// 状态.
    /// </summary>
    public AccessAppStatus Status { get; init; }

    /// <summary>
    /// 委托 SSO 登录回调地址列表（仅子系统行使用），空=未开放登录接入.
    /// </summary>
    public IReadOnlyList<string> CallbackUrls { get; init; } = [];

    /// <summary>
    /// 从实体构造.
    /// </summary>
    /// <param name="entity">应用接入实体.</param>
    /// <returns>返回调用者上下文.</returns>
    public static SubsysCallerContext From(AccessAppEntity entity) => new()
    {
        Id = entity.Id,
        Key = entity.Key,
        Name = entity.Name,
        AppType = entity.AppType,
        Status = entity.Status,
        CallbackUrls = MoAI.Subsystem.Services.SubsystemCallbacks.Parse(entity.CallbackUrls),
    };
}

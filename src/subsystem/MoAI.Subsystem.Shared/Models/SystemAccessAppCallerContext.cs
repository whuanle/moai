using MoAI.Database.Enums;

namespace MoAI.Subsystem.Models;

/// <summary>
/// 系统应用接入调用者上下文：中间件按 Bearer key 从数据库实时解析（停用/删除即时生效）.
/// </summary>
public class SystemAccessAppCallerContext
{
    /// <summary>
    /// 接入 id.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// 接入名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 功能范围.
    /// </summary>
    public SystemApiKeyScopes Scopes { get; init; }

    /// <summary>
    /// 状态.
    /// </summary>
    public AccessAppStatus Status { get; init; }
}

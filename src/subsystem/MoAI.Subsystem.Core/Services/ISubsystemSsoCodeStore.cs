namespace MoAI.Subsystem.Core.Services;

/// <summary>
/// 委托 SSO 一次性委托码存储.
/// </summary>
public interface ISubsystemSsoCodeStore
{
    /// <summary>
    /// 签发委托码，值绑定子系统与用户.
    /// </summary>
    /// <param name="subsystemId">子系统 id.</param>
    /// <param name="userId">用户 id.</param>
    /// <returns>返回委托码原文.</returns>
    Task<string> CreateAsync(Guid subsystemId, long userId);

    /// <summary>
    /// 原子消费委托码：不存在/已消费/已过期返回 null.
    /// </summary>
    /// <param name="code">委托码原文.</param>
    /// <returns>返回绑定的子系统与用户，无效为 null.</returns>
    Task<SubsystemSsoCodeStore.SsoCodePayload?> ConsumeAsync(string code);
}

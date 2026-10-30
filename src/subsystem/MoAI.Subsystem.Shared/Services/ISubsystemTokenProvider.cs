using MoAI.Database.Entities;

namespace MoAI.Subsystem.Services;

/// <summary>
/// 子系统 token 签发与校验：appid+密钥换取的机器面 JWT（audience = Server + |subsystem）.
/// </summary>
public interface ISubsystemTokenProvider
{
    /// <summary>
    /// 为子系统签发接入 token.
    /// </summary>
    /// <param name="subsystem">子系统实体.</param>
    /// <returns>返回 token 原文与有效期（秒）.</returns>
    (string AccessToken, int ExpiresIn) GenerateAccessToken(SubsystemEntity subsystem);

    /// <summary>
    /// 校验接入 token，成功时返回主体信息（子系统 id 与 appid）.
    /// </summary>
    /// <param name="token">token 原文.</param>
    /// <param name="subsystemId">子系统 id.</param>
    /// <param name="subsystemKey">子系统 appid.</param>
    /// <returns>有效返回 true.</returns>
    bool TryValidateAccessToken(string token, out Guid subsystemId, out string subsystemKey);

    /// <summary>
    /// 为子系统签发用户委托 token（用户经 MoAI 委托 SSO 登录子系统后使用，绑定子系统与用户）.
    /// </summary>
    /// <param name="subsystem">子系统实体.</param>
    /// <param name="userId">MoAI 用户 id.</param>
    /// <returns>返回 access/refresh token 与有效期（秒）.</returns>
    (string AccessToken, string RefreshToken, int ExpiresIn) GenerateUserTokens(SubsystemEntity subsystem, long userId);

    /// <summary>
    /// 校验用户委托 refresh token，成功时返回绑定的子系统 id 与用户 id.
    /// </summary>
    /// <param name="token">refresh token 原文.</param>
    /// <param name="subsystemId">绑定的子系统 id.</param>
    /// <param name="userId">绑定的用户 id.</param>
    /// <returns>有效返回 true.</returns>
    bool TryValidateUserRefreshToken(string token, out Guid subsystemId, out long userId);

    /// <summary>
    /// 为子系统签发机器面 access/refresh token 对（refresh 用于免重发密钥的轮换续期）.
    /// </summary>
    /// <param name="subsystem">子系统实体.</param>
    /// <returns>返回 access/refresh token 与 access 有效期（秒）.</returns>
    (string AccessToken, string RefreshToken, int ExpiresIn) GenerateMachineTokens(SubsystemEntity subsystem);

    /// <summary>
    /// 校验机器面 refresh token（typ=machine_refresh），成功时返回子系统 id 与 appid.
    /// </summary>
    /// <param name="token">refresh token 原文.</param>
    /// <param name="subsystemId">子系统 id.</param>
    /// <param name="subsystemKey">子系统 appid.</param>
    /// <returns>有效返回 true.</returns>
    bool TryValidateMachineRefreshToken(string token, out Guid subsystemId, out string subsystemKey);

    /// <summary>
    /// 为系统应用接入签发 access/refresh token（/api/subsys/system/* 协作面）.
    /// </summary>
    /// <param name="accessApp">系统级应用接入实体.</param>
    /// <returns>返回 access/refresh token 与 access 有效期（秒）.</returns>
    (string AccessToken, string RefreshToken, int ExpiresIn) GenerateSystemAppTokens(AccessAppEntity accessApp);

    /// <summary>
    /// 校验系统应用接入 access token（typ=system_app_access），成功时返回接入 id 与 appid.
    /// </summary>
    /// <param name="token">token 原文.</param>
    /// <param name="appId">接入 id.</param>
    /// <param name="appKey">接入 appid.</param>
    /// <returns>有效返回 true.</returns>
    bool TryValidateSystemAppAccessToken(string token, out Guid appId, out string appKey);

    /// <summary>
    /// 校验系统应用接入 refresh token（typ=system_app_refresh），成功时返回接入 id.
    /// </summary>
    /// <param name="token">refresh token 原文.</param>
    /// <param name="appId">接入 id.</param>
    /// <returns>有效返回 true.</returns>
    bool TryValidateSystemAppRefreshToken(string token, out Guid appId);
}

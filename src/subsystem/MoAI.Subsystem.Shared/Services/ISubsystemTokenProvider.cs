using MoAI.Database.Entities;

namespace MoAI.Subsystem.Services;

/// <summary>
/// 系统级接入 token 签发与校验：appid+密钥换取的机器面 JWT（audience = Server + |subsystem），子系统与应用接入共用一套.
/// </summary>
public interface ISubsystemTokenProvider
{
    /// <summary>
    /// 校验机器面 access token，成功时返回主体信息（接入 id 与 appid）.
    /// </summary>
    /// <param name="token">token 原文.</param>
    /// <param name="subsysAppId">接入 id.</param>
    /// <param name="subsysAppKey">接入 appid.</param>
    /// <returns>有效返回 true.</returns>
    bool TryValidateAccessToken(string token, out Guid subsysAppId, out string subsysAppKey);

    /// <summary>
    /// 签发用户委托 token（用户经 MoAI 委托 SSO 登录子系统后使用，绑定接入与用户）.
    /// </summary>
    /// <param name="subsysApp">接入实体（子系统行）.</param>
    /// <param name="userId">MoAI 用户 id.</param>
    /// <returns>返回 access/refresh token 与有效期（秒）.</returns>
    (string AccessToken, string RefreshToken, int ExpiresIn) GenerateUserTokens(AccessAppEntity subsysApp, long userId);

    /// <summary>
    /// 校验用户委托 refresh token，成功时返回绑定的接入 id 与用户 id.
    /// </summary>
    /// <param name="token">refresh token 原文.</param>
    /// <param name="subsysAppId">绑定的接入 id.</param>
    /// <param name="userId">绑定的用户 id.</param>
    /// <returns>有效返回 true.</returns>
    bool TryValidateUserRefreshToken(string token, out Guid subsysAppId, out long userId);

    /// <summary>
    /// 签发机器面 access/refresh token 对（refresh 用于免重发密钥的轮换续期）.
    /// </summary>
    /// <param name="subsysApp">接入实体.</param>
    /// <returns>返回 access/refresh token 与 access 有效期（秒）.</returns>
    (string AccessToken, string RefreshToken, int ExpiresIn) GenerateMachineTokens(AccessAppEntity subsysApp);

    /// <summary>
    /// 校验机器面 refresh token（typ=machine_refresh），成功时返回接入 id 与 appid.
    /// </summary>
    /// <param name="token">refresh token 原文.</param>
    /// <param name="subsysAppId">接入 id.</param>
    /// <param name="subsysAppKey">接入 appid.</param>
    /// <returns>有效返回 true.</returns>
    bool TryValidateMachineRefreshToken(string token, out Guid subsysAppId, out string subsysAppKey);
}

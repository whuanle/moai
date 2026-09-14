using MoAI.App.Models;
using MoAI.Database.Entities;

namespace MoAI.App.Services;

/// <summary>
/// 外部 token 服务：签发/校验 /api/external 的 access_token 与 refresh_token.
/// 外部 token 与内部 JWT 使用同一 RSA 私钥签发，但 audience 为 SystemOptions.Server + ExternalAuthDefaults.AudienceSuffix，
/// 内部接口的 JWT 校验（aud = SystemOptions.Server）天然拒绝外部 token.
/// </summary>
public interface IExternalTokenProvider
{
    /// <summary>
    /// 为应用接入签发应用 token，授权范围为接入配置的全部应用.
    /// </summary>
    /// <param name="accessApp">应用接入实体.</param>
    /// <returns>返回 access token、refresh token 与 access token 有效秒数.</returns>
    (string AccessToken, string RefreshToken, int ExpiresIn) GenerateAppTokens(AccessAppEntity accessApp);

    /// <summary>
    /// 为外部用户签发用户 token，授权范围为其绑定的单个应用.
    /// </summary>
    /// <param name="external">外部用户实体.</param>
    /// <param name="accessApp">来源应用接入实体，匿名 token 为 null.</param>
    /// <returns>返回 access token、refresh token 与 access token 有效秒数.</returns>
    (string AccessToken, string RefreshToken, int ExpiresIn) GenerateUserTokens(ExternalUserEntity external, AccessAppEntity? accessApp);

    /// <summary>
    /// 校验外部 access token（签名、issuer、audience、有效期、token 类型），并解析为 <see cref="ExternalTokenContext"/>.
    /// </summary>
    /// <param name="token">access token.</param>
    /// <param name="context">解析结果，校验失败为 null.</param>
    /// <returns>是否有效.</returns>
    bool TryValidateAccessToken(string token, out ExternalTokenContext? context);

    /// <summary>
    /// 校验外部 refresh token 并解析主体信息，用于刷新时恢复身份；授权范围在刷新时以数据库为准重建.
    /// </summary>
    /// <param name="token">refresh token.</param>
    /// <param name="context">解析结果，校验失败为 null.</param>
    /// <returns>是否有效.</returns>
    bool TryParseRefreshToken(string token, out ExternalTokenContext? context);
}

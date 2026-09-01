using MoAI.Auth.Models;

namespace MoAI.Auth.Services;

/// <summary>
/// 第三方登录用户信息解析服务.
/// <para>
/// 根据第三方 OAuth 授权回调的 code 解析出可用的统一用户标识
/// （如飞书 open_id、钉钉 union_id、openid），供登录与绑定复用。
/// </para>
/// </summary>
public interface IOAuthUserProfileService
{
    /// <summary>
    /// 解析第三方授权 code，获取统一用户信息.
    /// </summary>
    /// <param name="oAuthConnectionId">第三方认证方式 id（OauthConnection 表 id）.</param>
    /// <param name="code">授权回调得到的 code.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回 <see cref="OAuthBindUserProfile"/>，包含第三方用户唯一标识.</returns>
    Task<OAuthBindUserProfile> GetProfileAsync(Guid oAuthConnectionId, string code, CancellationToken cancellationToken);
}

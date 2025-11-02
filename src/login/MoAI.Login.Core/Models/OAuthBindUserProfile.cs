using MoAI.Infra.OAuth.Models;

namespace MoAI.Login.Models;

/// <summary>
/// 第三方登录统一抽象用户信息.
/// </summary>
public class OAuthBindUserProfile
{
    /// <summary>
    /// Oauth 供应商表 id.
    /// </summary>
    public Guid OAuthId { get; set; } = default!;

    /// <summary>
    /// 用户名字.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// OpenI 标识.
    /// </summary>
    public OpenIdUserProfile Profile { get; set; } = default!;

    /// <summary>
    /// 访问 token，目前用不上.
    /// </summary>
    public string AccessToken { get; set; } = default!;
}

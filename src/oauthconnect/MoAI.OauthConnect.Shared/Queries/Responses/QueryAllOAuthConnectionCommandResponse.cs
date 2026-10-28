namespace MoAI.OauthConnect.Queries.Responses;

/// <summary>
/// QueryAllOAuthConnectionCommandResponse.
/// </summary>
public class QueryAllOAuthConnectionCommandResponse
{
    /// <summary>
    /// 集合.
    /// </summary>
    public IReadOnlyCollection<QueryAllOAuthConnectionCommandResponseItem> Items { get; init; } = new List<QueryAllOAuthConnectionCommandResponseItem>();

    /// <summary>
    /// 回调地址（{WebUI}/oauth_login），管理员需将其配置到提供方后台的重定向/回调域名白名单.
    /// </summary>
    public string RedirectUri { get; init; } = default!;
}

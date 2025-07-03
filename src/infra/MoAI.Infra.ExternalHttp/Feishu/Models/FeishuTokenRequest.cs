using Refit;

namespace MoAI.Infra.Feishu.Models;

public class FeishuTokenRequest
{
    [AliasAs("grant_type")]
    public string GrantType { get; set; } = "authorization_code";

    [AliasAs("client_id")]
    public string ClientId { get; set; }

    [AliasAs("client_secret")]
    public string ClientSecret { get; set; }

    [AliasAs("code")]
    public string Code { get; set; }

    [AliasAs("redirect_uri")]
    public string RedirectUri { get; set; }

    [AliasAs("code_verifier")]
    public string CodeVerifier { get; set; }

    [AliasAs("scope")]
    public string Scope { get; set; }
}

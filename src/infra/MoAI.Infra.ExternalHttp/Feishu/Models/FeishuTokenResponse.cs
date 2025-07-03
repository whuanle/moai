using Refit;

namespace MoAI.Infra.Feishu.Models;

public class FeishuTokenResponse
{
    [AliasAs("code")]
    public int Code { get; set; }

    [AliasAs("access_token")]
    public string AccessToken { get; set; }

    [AliasAs("expires_in")]
    public int ExpiresIn { get; set; }

    [AliasAs("refresh_token")]
    public string RefreshToken { get; set; }

    [AliasAs("refresh_token_expires_in")]
    public int RefreshTokenExpiresIn { get; set; }

    [AliasAs("token_type")]
    public string TokenType { get; set; }

    [AliasAs("scope")]
    public string Scope { get; set; }

    [AliasAs("error")]
    public string Error { get; set; }

    [AliasAs("error_description")]
    public string ErrorDescription { get; set; }
}

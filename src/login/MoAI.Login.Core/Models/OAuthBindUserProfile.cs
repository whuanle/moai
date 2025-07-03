using MoAI.Infra.OAuth.Models;

namespace MoAI.Login.Models;

public class OAuthBindUserProfile
{
    public int OAuthId { get; set; } = default!;

    public OpenIdUserProfile Profile { get; set; } = default!;

    public string AccessToken { get; set; } = default!;
}

using MoAI.Infra.OAuth.Models;
using Refit;

namespace MoAI.Infra.OAuth;

public interface IOAuthClientAccessToken
{
    public HttpClient Client { get; }

    [Post("/{**path}")]
    [QueryUriFormat(UriFormat.Unescaped)]
    Task<OpenIdAuthorizationResponse> GetAccessTokenAsync(string path, [Body] OpenIdAuthorizationRequest request);

    [Get("/{**path}")]
    [QueryUriFormat(UriFormat.Unescaped)]
    Task<OpenIdUserProfile> GetUserInfoAsync(string path, [Query] string accessToken);
}

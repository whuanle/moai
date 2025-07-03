using MoAI.Infra.Feishu.Models;
using Refit;

namespace MoAI.Infra.Feishu;

public interface IFeishuClient
{
    public HttpClient Client { get; }

    [Post("/open-apis/authen/v2/oauth/token")]
    Task<FeishuTokenResponse> GetUserAccessTokenAsync(
        [Body(BodySerializationMethod.Serialized)] FeishuTokenRequest request);
}
using MediatR;
using MoAI.Login.Commands.Responses;

namespace MoAI.Login.Commands;

/// <summary>
/// 使用 OAuth 登录，用于第三方登录回调后触发接口.
/// </summary>
public class OAuthLoginCommand : IRequest<OAuthLoginCommandResponse>
{
    /// <summary>
    /// Code.
    /// </summary>
    public string Code { get; init; } = default!;

    /// <summary>
    /// id.
    /// </summary>
    public Guid OAuthId { get; init; } = default!;
}

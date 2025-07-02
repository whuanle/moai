using MediatR;
using MoAI.Login.Commands.Responses;

namespace MoAI.Login.Commands;

/// <summary>
/// 使用第三方账号一键注册.
/// </summary>
public class OAuthRegisterCommand : IRequest<LoginCommandResponse>
{
    /// <summary>
    /// 登录绑定 OAuth 用户 ID.
    /// </summary>
    public string OAuthBindId { get; init; } = default!;
}

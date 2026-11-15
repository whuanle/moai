using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Account.Commands;

/// <summary>
/// 强制下线用户：递增鉴权版本使该用户全部已签发 token 立即失效（不下发禁用状态，用户可重新登录）.
/// </summary>
public class ForceLogoutUserCommand : IRequest<EmptyCommandResponse>, IUserIdContext, IModelValidator<ForceLogoutUserCommand>
{
    /// <inheritdoc/>
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }

    /// <summary>
    /// 目标用户 id.
    /// </summary>
    public long UserId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<ForceLogoutUserCommand> validate)
    {
        // UserId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处只校验请求体字段.
    }
}

using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Account.Commands;

/// <summary>
/// 禁用/启用用户账号.
/// </summary>
public class UpdateUserIsDisableCommand : IRequest<EmptyCommandResponse>, IUserIdContext, IModelValidator<UpdateUserIsDisableCommand>
{
    /// <inheritdoc/>
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }

    /// <summary>
    /// 目标用户 id.
    /// </summary>
    public long UserId { get; init; }

    /// <summary>
    /// 是否禁用.
    /// </summary>
    public bool IsDisable { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateUserIsDisableCommand> validate)
    {
        // UserId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处只校验请求体字段.
    }
}

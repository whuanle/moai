using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Account.Commands;

/// <summary>
/// 设置/取消用户的管理员角色，仅超级管理员可操作.
/// </summary>
public class UpdateUserIsAdminCommand : IRequest<EmptyCommandResponse>, IUserIdContext, IModelValidator<UpdateUserIsAdminCommand>
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
    /// 是否设为管理员.
    /// </summary>
    public bool IsAdmin { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateUserIsAdminCommand> validate)
    {
        // UserId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处只校验请求体字段.
    }
}

using FluentValidation;
using MediatR;
using MoAI.App.AppStore.Models;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.App.Apps.CommonApp.Queries;

/// <summary>
/// 检查用户是否有权使用应用.
/// </summary>
public class CheckUserAppPermissionCommand : IUserIdContext, IRequest<CheckUserAppPermissionCommandResponse>, IModelValidator<CheckUserAppPermissionCommand>
{
    /// <inheritdoc/>
    public int ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }

    /// <summary>
    /// 应用 id.
    /// </summary>
    public Guid AppId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<CheckUserAppPermissionCommand> validate)
    {
        validate.RuleFor(x => x.AppId)
            .NotEmpty().WithMessage("应用 ID 不能为空.");
    }
}

using FluentValidation;
using MediatR;
using MoAI.App.Apps.CommonApp.Responses;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.App.Apps.CommonApp.Queries;

/// <summary>
/// 检查用户是否有权访问应用.
/// </summary>
public class CheckAppAccessCommand : IUserIdContext, IRequest<CheckAppAccessCommandResponse>, IModelValidator<CheckAppAccessCommand>
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
    public static void Validate(AbstractValidator<CheckAppAccessCommand> validate)
    {
        validate.RuleFor(x => x.AppId)
            .NotEmpty().WithMessage("应用 ID 不能为空.");
    }
}

using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using MoAI.Infra.Defaults;
using MoAI.Infra.Models;

namespace MoAI.Login.Commands;

/// <summary>
/// 删除认证方式.
/// </summary>
public class DeleteOAuthConnectionCommand : IRequest<EmptyCommandResponse>, IModelValidator<DeleteOAuthConnectionCommand>
{
    /// <summary>
    /// id.
    /// </summary>
    public Guid OAuthConnectionId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<DeleteOAuthConnectionCommand> validate)
    {
        validate.RuleFor(x => x.OAuthConnectionId).NotEmpty().WithMessage("ID不能为空.");
    }
}
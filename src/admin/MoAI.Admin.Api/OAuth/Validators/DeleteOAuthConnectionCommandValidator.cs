using FastEndpoints;
using FluentValidation;
using MoAI.Login.Commands;

namespace MoAI.Admin.OAuth.Validators;

/// <summary>
/// DeleteOAuthConnectionCommandValidator.
/// </summary>
public class DeleteOAuthConnectionCommandValidator : AbstractValidator<DeleteOAuthConnectionCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteOAuthConnectionCommandValidator"/> class.
    /// </summary>
    public DeleteOAuthConnectionCommandValidator()
    {
        RuleFor(x => x.OAuthConnectionId).NotEmpty().WithMessage("ID不能为空.");
    }
}

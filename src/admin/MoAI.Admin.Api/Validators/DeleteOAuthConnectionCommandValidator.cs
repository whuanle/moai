using FastEndpoints;
using FluentValidation;
using MoAI.Login.Commands;

namespace MoAI.Admin.Validators;

public class DeleteOAuthConnectionCommandValidator : Validator<DeleteOAuthConnectionCommand>
{
    public DeleteOAuthConnectionCommandValidator()
    {
        RuleFor(x => x.OAuthConnectionId).NotEmpty().WithMessage("ID不能为空.");
    }
}

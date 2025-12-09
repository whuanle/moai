using FastEndpoints;
using FluentValidation;
using MoAI.Login.Commands;

namespace MoAI.Login.Validators;

/// <inheritdoc/>
public class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RefreshTokenCommandValidator"/> class.
    /// </summary>
    public RefreshTokenCommandValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}

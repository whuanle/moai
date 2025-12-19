using FluentValidation;
using MediatR;
using MoAI.Login.Commands.Responses;

namespace MoAI.Login.Commands;

/// <summary>
/// 刷新 token.
/// </summary>
public class RefreshTokenCommand : IRequest<RefreshTokenCommandResponse>, IModelValidator<RefreshTokenCommand>
{
    /// <summary>
    /// 刷新令牌.
    /// </summary>
    public string RefreshToken { get; init; } = default!;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<RefreshTokenCommand> validate)
    {
        validate.RuleFor(x => x.RefreshToken).NotEmpty();
    }
}

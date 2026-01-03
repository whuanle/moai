using FluentValidation;
using MediatR;
using MoAI.External.Commands.Responses;

namespace MoAI.External.Commands;

/// <summary>
/// 外部接入刷新 Token.
/// </summary>
public class ExternalRefreshTokenCommand : IRequest<ExternalRefreshTokenCommandResponse>, IModelValidator<ExternalRefreshTokenCommand>
{
    /// <summary>
    /// 刷新令牌.
    /// </summary>
    public string RefreshToken { get; init; } = default!;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<ExternalRefreshTokenCommand> validate)
    {
        validate.RuleFor(x => x.RefreshToken).NotEmpty();
    }
}

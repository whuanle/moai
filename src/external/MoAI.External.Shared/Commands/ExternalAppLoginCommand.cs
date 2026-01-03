using FluentValidation;
using MediatR;
using MoAI.External.Commands.Responses;

namespace MoAI.External.Commands;

/// <summary>
/// 外部应用登录，通过 AppId 和 Key 验证身份后颁发 Token.
/// </summary>
public class ExternalAppLoginCommand : IRequest<ExternalAppLoginCommandResponse>, IModelValidator<ExternalAppLoginCommand>
{
    /// <summary>
    /// 应用ID.
    /// </summary>
    public Guid AppId { get; init; }

    /// <summary>
    /// 应用密钥.
    /// </summary>
    public string Key { get; init; } = default!;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<ExternalAppLoginCommand> validate)
    {
        validate.RuleFor(x => x.AppId).NotEmpty();
        validate.RuleFor(x => x.Key).NotEmpty();
    }
}

using FluentValidation;
using MediatR;
using MoAI.External.Commands.Responses;

namespace MoAI.External.Commands;

/// <summary>
/// 外部用户登录，为外部用户颁发 Token.
/// </summary>
public class ExternalUserLoginCommand : IRequest<ExternalUserLoginCommandResponse>, IModelValidator<ExternalUserLoginCommand>
{
    /// <summary>
    /// 外部用户ID（由外部系统提供）.
    /// </summary>
    public string ExternalUserId { get; init; } = default!;

    /// <summary>
    /// 外部用户名.
    /// </summary>
    public string UserName { get; init; } = default!;

    /// <summary>
    /// 外部用户昵称.
    /// </summary>
    public string NickName { get; init; } = default!;

    /// <summary>
    /// 外部用户邮箱.
    /// </summary>
    public string Email { get; init; } = string.Empty;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<ExternalUserLoginCommand> validate)
    {
        validate.RuleFor(x => x.ExternalUserId).NotEmpty();
        validate.RuleFor(x => x.UserName).NotEmpty();
        validate.RuleFor(x => x.NickName).NotEmpty();
    }
}

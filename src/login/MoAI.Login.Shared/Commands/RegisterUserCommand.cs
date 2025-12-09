using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Login.Commands;

/// <summary>
/// 注册用户.
/// </summary>
public class RegisterUserCommand : IRequest<SimpleInt>, IModelValidator<RegisterUserCommand>
{
    /// <summary>
    /// 用户名.
    /// </summary>
    public string UserName { get; init; } = default!;

    /// <summary>
    /// 邮箱.
    /// </summary>
    public string Email { get; init; } = default!;

    /// <summary>
    /// 密码，接口请求时，使用 RSA 公钥加密密码.
    /// </summary>
    public string Password { get; init; } = default!;

    /// <summary>
    /// 昵称.
    /// </summary>
    public string NickName { get; init; } = default!;

    /// <summary>
    /// 手机号.
    /// </summary>
    public string Phone { get; init; } = default!;

    /// <inheritdoc/>
    public void Validate(AbstractValidator<RegisterUserCommand> validate)
    {
        validate.RuleFor(x => x.UserName).NotEmpty().MinimumLength(5).MaximumLength(20).WithMessage("用户名 5-20 字符.");
        validate.RuleFor(x => x.Email).NotEmpty().EmailAddress().MinimumLength(5).MaximumLength(50).WithMessage("邮箱 5-50 字符.");
        validate.RuleFor(x => x.Password).NotEmpty(); // .MinimumLength(8).MaximumLength(30).Matches("(?=.*[A-Za-z])(?=.*\d)[A-Za-z\d\S]{8,20}$").WithMessage("密码 8-30 长度，并包含数字+字母+特殊字符.");
        validate.RuleFor(x => x.NickName).NotEmpty().MinimumLength(3).MaximumLength(20).WithMessage("昵称 3-20 字符.");
        validate.RuleFor(x => x.Phone).NotEmpty().Matches(@"^(?:\+?1)?\d{10,15}$").WithMessage("手机号格式错误.");
    }
}
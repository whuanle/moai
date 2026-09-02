using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Account.Commands;

/// <summary>
/// 更新用户基本信息.
/// </summary>
public class UpdateUserInfoCommand : IRequest<EmptyCommandResponse>, IUserIdContext, IModelValidator<UpdateUserInfoCommand>
{
    /// <inheritdoc/>
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }

    /// <summary>
    /// 昵称.
    /// </summary>
    public string? NickName { get; set; }

    /// <summary>
    /// 手机号.
    /// </summary>
    public string? Phone { get; set; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateUserInfoCommand> validate)
    {
        validate.RuleFor(x => x.NickName)
            .MaximumLength(50).WithMessage("昵称最长 50 个字符.")
            .When(x => !string.IsNullOrWhiteSpace(x.NickName));
        validate.RuleFor(x => x.Phone)
            .Matches(@"^(?:\+?1)?\d{10,15}$").WithMessage("手机号格式错误.")
            .When(x => !string.IsNullOrEmpty(x.Phone));
    }
}

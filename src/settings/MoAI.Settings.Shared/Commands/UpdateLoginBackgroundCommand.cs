using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Settings.Commands;

/// <summary>
/// 更新登录页背景图（ObjectKey 为空表示恢复默认背景）.
/// </summary>
public class UpdateLoginBackgroundCommand : IRequest<EmptyCommandResponse>, IModelValidator<UpdateLoginBackgroundCommand>
{
    /// <summary>
    /// 背景图在存储中的 ObjectKey，空串恢复默认.
    /// </summary>
    public string ObjectKey { get; init; } = string.Empty;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateLoginBackgroundCommand> validate)
    {
        validate.RuleFor(x => x.ObjectKey)
            .MaximumLength(500).WithMessage("登录页背景图文件标识过长.")
            .When(x => !string.IsNullOrWhiteSpace(x.ObjectKey));
    }
}

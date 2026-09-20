using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Settings.Commands;

/// <summary>
/// 更新网站 Logo（ObjectKey 为空表示恢复默认 Logo）.
/// </summary>
public class UpdateSystemLogoCommand : IRequest<EmptyCommandResponse>, IModelValidator<UpdateSystemLogoCommand>
{
    /// <summary>
    /// Logo 图片在存储中的 ObjectKey，空串恢复默认.
    /// </summary>
    public string ObjectKey { get; init; } = string.Empty;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateSystemLogoCommand> validate)
    {
        validate.RuleFor(x => x.ObjectKey)
            .MaximumLength(500).WithMessage("Logo 文件标识过长.")
            .When(x => !string.IsNullOrWhiteSpace(x.ObjectKey));
    }
}

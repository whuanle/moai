using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Prompt.Commands;

/// <summary>
/// 创建提示词分类.
/// </summary>
public class CreatePromptClassifyCommand : IRequest<SimpleInt>, IModelValidator<CreatePromptClassifyCommand>
{
    /// <summary>
    /// 分类名称.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 分类描述.
    /// </summary>
    public string? Description { get; init; } = string.Empty;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<CreatePromptClassifyCommand> validate)
    {
        validate.RuleFor(x => x.Name)
            .NotEmpty().WithMessage("名称不能为空")
            .MaximumLength(50).WithMessage("名称不能超过50个字符");
    }
}

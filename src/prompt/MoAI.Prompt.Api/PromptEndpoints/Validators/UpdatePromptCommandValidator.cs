using FluentValidation;
using MoAI.Prompt.Commands;

namespace MoAI.Prompt.PromptEndpoints.Validators;

/// <summary>
/// UpdatePromptCommandValidator.
/// </summary>
public class UpdatePromptCommandValidator : AbstractValidator<UpdatePromptCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdatePromptCommandValidator"/> class.
    /// </summary>
    public UpdatePromptCommandValidator()
    {
        RuleFor(x => x.PromptId).NotEmpty().WithMessage("提示词ID不能为空");
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("名称不能为空")
            .MaximumLength(50).WithMessage("名称不能超过50个字符");
        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("描述不能为空")
            .MaximumLength(200).WithMessage("描述不能超过200个字符");
        RuleFor(x => x.PromptClassId).NotEmpty().WithMessage("分类ID不能为空");
    }
}
using FluentValidation;
using MoAI.Prompt.PromptEndpoints.Models;

namespace MoAI.Prompt.PromptEndpoints.Validators;

/// <summary>
/// AiOptimizePromptRequestValidator.
/// </summary>
public class AiOptimizePromptRequestValidator : AbstractValidator<AiOptimizePromptRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AiOptimizePromptRequestValidator"/> class.
    /// </summary>
    public AiOptimizePromptRequestValidator()
    {
        RuleFor(x => x.AiModelId).NotEmpty().WithMessage("模型id不正确");

        RuleFor(x => x.SourcePrompt)
            .NotEmpty().WithMessage("提示词不能为空")
            .MaximumLength(2000).WithMessage("提示词不能超过2000个字符");
    }
}

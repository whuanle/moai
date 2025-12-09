using FluentValidation;
using MediatR;

namespace MoAI.Prompt.Models;

public class AiOptimizePromptRequest : IModelValidator<AiOptimizePromptRequest>
{
    /// <summary>
    /// AI 模型 id.
    /// </summary>
    public int AiModelId { get; init; }

    /// <summary>
    /// 用户原本的提示词
    /// </summary>
    public string SourcePrompt { get; init; }

    /// <inheritdoc/>
    public void Validate(AbstractValidator<AiOptimizePromptRequest> validate)
    {
        validate.RuleFor(x => x.AiModelId).NotEmpty().WithMessage("模型id不正确");

        validate.RuleFor(x => x.SourcePrompt)
            .NotEmpty().WithMessage("提示词不能为空")
            .MaximumLength(2000).WithMessage("提示词不能超过2000个字符");
    }
}

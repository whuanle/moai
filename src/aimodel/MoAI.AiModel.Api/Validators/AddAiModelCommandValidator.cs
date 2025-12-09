using FastEndpoints;
using FluentValidation;
using MoAI.AiModel.Commands;

namespace MoAI.AiModel.Validators;

/// <summary>
/// AddAiModelCommandValidator.
/// </summary>
public class AddAiModelCommandValidator : AbstractValidator<AddAiModelCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AddAiModelCommandValidator"/> class.
    /// </summary>
    public AddAiModelCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("模型名称不能为空")
            .MaximumLength(50).WithMessage("模型名称不能超过50个字符");
        RuleFor(x => x.Key)
            .NotEmpty().WithMessage("密钥不能为空");
        RuleFor(x => x.Endpoint)
            .NotEmpty().WithMessage("请求端点不能为空")
            .MaximumLength(500).WithMessage("请求端点不能超过255个字符");
    }
}

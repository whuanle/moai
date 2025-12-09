using FastEndpoints;
using FluentValidation;
using MoAI.AiModel.Commands;

namespace MoAI.AiModel.Validators;

/// <summary>
/// UpdateAiModelCommandValidator.
/// </summary>
public class UpdateAiModelCommandValidator : AbstractValidator<UpdateAiModelCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateAiModelCommandValidator"/> class.
    /// </summary>
    public UpdateAiModelCommandValidator()
    {
        RuleFor(x => x.AiModelId)
            .NotEmpty().WithMessage("模型id有误");

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

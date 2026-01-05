using FluentValidation;
using MediatR;
using MoAI.AI.Models;
using MoAI.Infra.Defaults;
using MoAI.Infra.Models;

namespace MoAI.AiModel.Commands;

/// <summary>
/// 添加 AI 模型.
/// </summary>
public class AddAiModelCommand : AiEndpoint, IRequest<SimpleInt>, IModelValidator<AddAiModelCommand>
{
    /// <summary>
    /// 公开给用户使用.
    /// </summary>
    public bool IsPublic { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<AddAiModelCommand> validate)
    {
        validate.RuleFor(x => x.Title)
            .NotEmpty().WithMessage("模型名称不能为空")
            .MaximumLength(50).WithMessage("模型名称不能超过50个字符");
        validate.RuleFor(x => x.Key)
            .NotEmpty().WithMessage("密钥不能为空");
        validate.RuleFor(x => x.Endpoint)
            .NotEmpty().WithMessage("请求端点不能为空")
            .MaximumLength(500).WithMessage("请求端点不能超过255个字符");
    }
}

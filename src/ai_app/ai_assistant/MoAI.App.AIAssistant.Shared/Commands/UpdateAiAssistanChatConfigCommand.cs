using FluentValidation;
using MediatR;
using MoAI.App.AIAssistant.Models;
using MoAI.Infra.Defaults;
using MoAI.Infra.Models;

namespace MoAI.App.AIAssistant.Handlers;

/// <summary>
/// 更新 AI 对话参数.
/// </summary>
public class UpdateAiAssistanChatConfigCommand : AIAssistantChatObject, IRequest<EmptyCommandResponse>, IModelValidator<UpdateAiAssistanChatConfigCommand>
{
    /// <summary>
    /// 对话 id.
    /// </summary>
    public Guid ChatId { get; init; }

    /// <summary>
    /// AI 的头像.
    /// </summary>
    public string AiAvatar { get; init; } = string.Empty;

    /// <inheritdoc/>
    public void Validate(AbstractValidator<UpdateAiAssistanChatConfigCommand> validate)
    {
        validate.RuleFor(x => x.ChatId)
            .NotEmpty().WithMessage("对话id不正确.")
            .Must(x => x != Guid.Empty).WithMessage("对话id不正确.");

        validate.RuleFor(x => x.AiAvatar)
            .NotEmpty().WithMessage("AI 头像不能为空.")
            .MaximumLength(10).WithMessage("AI 头像长度不能超过10个字符.");

        validate.RuleFor(x => x.Title)
            .NotEmpty().WithMessage("对话标题不能为空.")
            .MaximumLength(100).WithMessage("对话标题长度不能超过100个字符.");

        validate.RuleFor(x => x.Prompt)
            .NotEmpty().WithMessage("提示词不能为空.")
            .MaximumLength(2000).WithMessage("提示词长度不能超过2000个字符.");

        validate.RuleFor(x => x.ModelId)
            .GreaterThan(0).WithMessage("模型 ID 错误.");
    }
}

using FluentValidation;
using MediatR;
using MoAI.App.AIAssistant.Commands.Responses;
using MoAI.App.AIAssistant.Models;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAIChat.Core.Handlers;

/// <summary>
/// 创建新的对话.
/// </summary>
public class CreateAiAssistantChatCommand : AIAssistantChatObject, IUserIdContext, IRequest<CreateAiAssistantChatCommandResponse>, IModelValidator<CreateAiAssistantChatCommand>
{
    /// <inheritdoc/>
    public int ContextUserId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<CreateAiAssistantChatCommand> validate)
    {
        validate.RuleFor(x => x.Title)
            .NotEmpty().WithMessage("对话标题不能为空.")
            .MaximumLength(100).WithMessage("对话标题长度不能超过100个字符.");

        validate.RuleFor(x => x.Prompt)
            .MaximumLength(3000).WithMessage("提示词长度不能超过3000个字符.");

        validate.RuleFor(x => x.ModelId)
            .GreaterThan(0).WithMessage("模型 ID 错误.");

        validate.RuleForEach(x => x.ExecutionSettings)
            .Must(kv => !string.IsNullOrWhiteSpace(kv.Key)).WithMessage("执行配置的键不能为空.");

        validate.RuleForEach(x => x.CustomPluginIds).NotNull();
        validate.RuleForEach(x => x.NativePluginIds).NotNull();
        validate.RuleForEach(x => x.ToolPluginIds).NotNull();
    }
}

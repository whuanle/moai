using FluentValidation;
using MediatR;
using MoAI.App.AIAssistant.Commands.Responses;
using MoAI.App.AIAssistant.Models;

namespace MoAIChat.Core.Handlers;

/// <summary>
/// 创建新的对话，要使用 ProcessingAiAssistantChatCommand 发起对话，此接口只用于新建第一条记录.
/// </summary>
public class CreateAiAssistantChatCommand : AIAssistantChatObject, IRequest<CreateAiAssistantChatCommandResponse>, IModelValidator<CreateAiAssistantChatCommand>
{
    /// <summary>
    /// 用户 id.
    /// </summary>
    public int UserId { get; init; }

    /// <inheritdoc/>
    public void Validate(AbstractValidator<CreateAiAssistantChatCommand> validate)
    {
        validate.RuleFor(x => x.Title)
            .NotEmpty().WithMessage("对话标题不能为空.")
            .MaximumLength(100).WithMessage("对话标题长度不能超过100个字符.");

        validate.RuleFor(x => x.Prompt)
            .MaximumLength(2000).WithMessage("提示词长度不能超过2000个字符.");

        validate.RuleFor(x => x.ModelId)
            .GreaterThan(0).WithMessage("模型 ID 错误.");
    }
}

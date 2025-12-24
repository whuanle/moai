using FluentValidation;
using MediatR;
using MoAI.App.AIAssistant.Models;
using MoAI.Infra.Defaults;
using MoAI.Infra.Models;

namespace MoAI.App.AIAssistant.Handlers;

/// <summary>
/// 更新 AI 对话参数.
/// </summary>
public class UpdateAiAssistanChatConfigCommand : IUserIdContext, IRequest<EmptyCommandResponse>, IModelValidator<UpdateAiAssistanChatConfigCommand>
{
    /// <summary>
    /// 对话 id.
    /// </summary>
    public Guid ChatId { get; init; }

    /// <inheritdoc/>
    public int ContextUserId { get; init; }

    /// <summary>
    /// 头像图标.
    /// </summary>
    public string Avatar { get; init; } = string.Empty;

    /// <summary>
    /// 话题名称.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// 提示词，第一次对话时带上，如果后续不需要修改则不需要再次传递.
    /// </summary>
    public string? Prompt { get; init; } = string.Empty;

    /// <summary>
    /// 要使用的 AI 模型.
    /// </summary>
    public int ModelId { get; init; }

    /// <summary>
    /// 要使用的知识库列表，可使用已加入的或公开的知识库.
    /// </summary>
    public IReadOnlyCollection<int> WikiIds { get; init; } = Array.Empty<int>();

    /// <summary>
    /// 要使用的插件列表，填插件的 Key，Tool 类插件的 Key 就是其对应的模板 Key.
    /// </summary>
    public IReadOnlyCollection<string> Plugins { get; init; } = Array.Empty<string>();

    /// <summary>
    /// 配置，字典适配不同的 AI 模型.
    /// </summary>
    public IReadOnlyCollection<KeyValueString> ExecutionSettings { get; init; } = Array.Empty<KeyValueString>();

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateAiAssistanChatConfigCommand> validate)
    {
        validate.RuleFor(x => x.ChatId)
            .NotEmpty().WithMessage("对话id不正确.")
            .Must(x => x != Guid.Empty).WithMessage("对话id不正确.");

        validate.RuleFor(x => x.Avatar)
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

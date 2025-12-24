using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Plugin.Models;

namespace MoAI.App.AIAssistant.Models;

/// <summary>
/// 对话参数.
/// </summary>
public class AIAssistantChatObject : IModelValidator<AIAssistantChatObject>
{
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
    /// 要使用的插件列表.
    /// </summary>
    public IReadOnlyCollection<PluginKeyValue> Plugins { get; init; } = Array.Empty<PluginKeyValue>();

    /// <summary>
    /// 配置，字典适配不同的 AI 模型.
    /// </summary>
    public IReadOnlyCollection<KeyValueString> ExecutionSettings { get; init; } = Array.Empty<KeyValueString>();

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<AIAssistantChatObject> validate)
    {
        validate.RuleFor(x => x.Prompt)
            .MaximumLength(2000).WithMessage("提示词长度不能超过2000个字符.");

        validate.RuleFor(x => x.ModelId)
            .GreaterThan(0).WithMessage("模型 ID 错误.");

        validate.RuleForEach(x => x.ExecutionSettings)
            .Must(kv => !string.IsNullOrWhiteSpace(kv.Key)).WithMessage("执行配置的键不能为空.");

        validate.RuleForEach(x => x.Plugins).NotNull();
    }
}

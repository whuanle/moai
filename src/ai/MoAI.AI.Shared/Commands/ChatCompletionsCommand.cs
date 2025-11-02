using MediatR;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using MoAI.AI.Models;
using MoAI.AiModel.Models;
using MoAI.Infra.Models;

namespace MoAI.AI.Commands;

/// <summary>
/// 聊天.
/// </summary>
public class ChatCompletionsCommand : IStreamRequest<IOpenAIChatCompletionsObject>
{
    /// <summary>
    /// Chat id.
    /// </summary>
    public Guid ChatId { get; init; }

    /// <summary>
    /// 对话 AI 信息.
    /// </summary>
    public AiEndpoint Endpoint { get; init; } = default!;

    /// <summary>
    /// 历史对话或者上下文信息，创建对话时，如果有提示词，则第一个对话就是提示词.
    /// </summary>
    public ChatHistory ChatHistory { get; init; } = new ChatHistory();

    /// <summary>
    /// 插件列表.
    /// </summary>
    public IReadOnlyCollection<KernelPlugin> Plugins { get; init; } = new List<KernelPlugin>();

    /// <summary>
    /// 执行属性信息.
    /// </summary>
    public IReadOnlyCollection<KeyValueString> ExecutionSetting { get; init; } = Array.Empty<KeyValueString>();
}
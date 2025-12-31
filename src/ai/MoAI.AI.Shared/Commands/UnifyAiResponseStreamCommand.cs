using MediatR;
using Microsoft.SemanticKernel;
using MoAI.AI.Models;

namespace MoAI.AI.Commands;

/// <summary>
/// 统一 AI 响应处理.
/// </summary>
public class UnifyAiResponseStreamCommand : IStreamRequest<AiProcessingChatItem>
{
    /// <summary>
    /// 流式响应内容.
    /// </summary>
    public IAsyncEnumerable<StreamingChatMessageContent> ResponseStream { get; init; } = default!;

    /// <summary>
    /// 对话上下文.
    /// </summary>
    public ProcessingAiAssistantChatContext ChatContext { get; init; } = default!;
}

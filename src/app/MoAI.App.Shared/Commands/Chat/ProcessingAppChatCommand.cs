using MediatR;
using MoAI.AI.Models;
using MoAI.Infra.Models;

namespace MoAI.App.Commands.Chat;

/// <summary>
/// 应用对话调试.
/// </summary>
public class ProcessingAppChatCommand : IStreamRequest<AiProcessingChatItem>, IUserIdContext
{
    /// <inheritdoc/>
    public int ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }

    /// <summary>
    /// 团队id.
    /// </summary>
    public int TeamId { get; init; }

    /// <summary>
    /// 应用id.
    /// </summary>
    public Guid AppId { get; init; }

    /// <summary>
    /// 用户的提问.
    /// </summary>
    public string? Content { get; init; } = string.Empty;

    /// <summary>
    /// 图片 key，需要先调用接口上传图片.
    /// </summary>
    public string? FileKey { get; init; }

    /// <summary>
    /// 对话 id，第一次对话时可不传，后续对话需传入，用于关联上下文.
    /// </summary>
    public Guid? ChatId { get; init; }
}
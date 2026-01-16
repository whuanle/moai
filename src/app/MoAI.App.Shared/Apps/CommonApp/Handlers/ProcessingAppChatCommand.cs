using MediatR;
using MoAI.AI.Models;
using MoAI.Infra.Models;

namespace MoAI.App.Apps.CommonApp.Handlers;

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
    public string Question { get; init; } = string.Empty;

    /// <summary>
    /// 图片 key，需要先调用接口上传图片.
    /// </summary>
    public string? FileKey { get; init; }

    /// <summary>
    /// 对话 id.
    /// </summary>
    public Guid ChatId { get; init; }
}
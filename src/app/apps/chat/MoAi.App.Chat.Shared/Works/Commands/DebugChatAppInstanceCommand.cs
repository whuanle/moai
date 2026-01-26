using MediatR;
using MoAI.AI.Models;
using MoAI.App.Chat.Works.Models;
using MoAI.Infra.Models;

namespace MoAI.App.Chat.Works.Commands;

/// <summary>
/// 获得该用户对应的应用调试对话实例 id.
/// </summary>
public class QueryDebugChatAppInstanceCommand : IUserIdContext, IRequest<QueryDebugChatAppInstanceCommandResponse>
{
    /// <inheritdoc/>
    public int ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }

    /// <summary>
    /// 团队 id.
    /// </summary>
    public int TeamId { get; init; }

    /// <summary>
    /// 应用 id.
    /// </summary>
    public Guid AppId { get; init; }
}

public class QueryDebugChatAppInstanceCommandResponse
{
    /// <summary>
    /// 对话历史记录.
    /// </summary>
    public IReadOnlyCollection<AppChatHistoryItem> ChatHistory { get; init; } = Array.Empty<AppChatHistoryItem>();
}

/// <summary>
/// 清空调试对话的记录.
/// </summary>
public class ClearDebugChatAppInstanceCommand : IUserIdContext, IRequest<EmptyCommandResponse>
{
    /// <inheritdoc/>
    public int ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }

    /// <summary>
    /// 团队 id.
    /// </summary>
    public int TeamId { get; init; }

    /// <summary>
    /// 应用 id.
    /// </summary>
    public Guid AppId { get; init; }
}

/// <summary>
/// 应用调试对话（不存储到数据库）.
/// </summary>
public class DebugChatAppInstanceCommand : IStreamRequest<AiProcessingChatItem>, IUserIdContext
{
    /// <inheritdoc/>
    public int ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }

    /// <summary>
    /// 正在调试的应用 id.
    /// </summary>
    public Guid AppId { get; init; }

    /// <summary>
    /// 团队id.
    /// </summary>
    public int TeamId { get; init; }

    /// <summary>
    /// 用户的提问.
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// 问题.
    /// </summary>
    public string Question { get; init; } = string.Empty;

    /// <summary>
    /// 提示词.
    /// </summary>
    public string? Prompt { get; init; }

    /// <summary>
    /// 文件 key.
    /// </summary>
    public string? FileKey { get; init; }

    /// <summary>
    /// 对话使用的模型 id.
    /// </summary>
    public int ModelId { get; init; }

    /// <summary>
    /// 要使用的知识库id列表.
    /// </summary>
    public IReadOnlyCollection<int>? WikiIds { get; init; }

    /// <summary>
    /// 要使用的插件列表.
    /// </summary>
    public IReadOnlyCollection<string>? Plugins { get; init; }

    /// <summary>
    /// 对话影响参数.
    /// </summary>
    public IReadOnlyCollection<KeyValueString>? ExecutionSettings { get; init; }
}

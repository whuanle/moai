using MediatR;
using MoAI.AI.Models;
using MoAI.Infra.Models;

namespace MoAI.App.Apps.CommonApp.Handlers;

/// <summary>
/// 应用调试对话（不存储到数据库）.
/// </summary>
public class DebugAppChatCommand : IStreamRequest<AiProcessingChatItem>, IUserIdContext
{
    /// <inheritdoc/>
    public int ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }

    /// <summary>
    /// 临时会话 id.
    /// </summary>
    public Guid ChatId { get; init; }

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
    public string Question { get; init; }

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

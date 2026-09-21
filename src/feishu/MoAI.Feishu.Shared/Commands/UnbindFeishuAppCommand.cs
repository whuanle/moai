using System.Text.Json.Serialization;
using MediatR;
using MoAI.Database.Enums;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.Feishu.Commands;

/// <summary>
/// 解除飞书应用绑定，需要团队 Admin 及以上角色.
/// </summary>
public class UnbindFeishuAppCommand : IRequest<EmptyCommandResponse>, IUserIdContext
{
    /// <summary>
    /// 飞书应用记录 id（来自路由）.
    /// </summary>
    public Guid FeishuAppId { get; init; }

    /// <summary>
    /// 要解除的渠道类型；为空表示解除该飞书应用的全部绑定（知识库外部源等订阅型渠道可一对多，需按渠道指定）.
    /// </summary>
    public FeishuChannelType? ChannelType { get; init; }

    /// <summary>
    /// 要解除的渠道记录 id；与 <see cref="ChannelType"/> 同时指定时只解除该条绑定.
    /// </summary>
    public string? ChannelId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }
}

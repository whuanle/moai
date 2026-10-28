using System.Text.Json.Serialization;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.DingTalk.Commands;

/// <summary>
/// 删除钉钉应用连接，需要团队 Admin 及以上角色；删除时同时解除其全部绑定并断开长连接.
/// </summary>
public class DeleteDingTalkAppCommand : IRequest<EmptyCommandResponse>, IUserIdContext
{
    /// <summary>
    /// 钉钉应用记录 id（来自路由）.
    /// </summary>
    public Guid DingTalkAppId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }
}

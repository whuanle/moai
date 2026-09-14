using System.Text.Json.Serialization;
using MediatR;
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

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }
}

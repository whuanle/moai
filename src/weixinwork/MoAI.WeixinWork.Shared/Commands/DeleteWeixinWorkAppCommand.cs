using System.Text.Json.Serialization;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.WeixinWork.Commands;

/// <summary>
/// 删除企业微信应用连接，需要团队 Admin 及以上角色；删除时同时解除其全部绑定.
/// </summary>
public class DeleteWeixinWorkAppCommand : IRequest<EmptyCommandResponse>, IUserIdContext
{
    /// <summary>
    /// 企业微信应用记录 id（来自路由）.
    /// </summary>
    public Guid WeixinWorkAppId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }
}

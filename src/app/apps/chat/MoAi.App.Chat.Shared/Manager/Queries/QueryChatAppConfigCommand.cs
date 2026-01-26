using MediatR;
using MoAI.App.Chat.Manager.Models;
using MoAI.Infra.Models;

namespace MoAI.App.Chat.Manager.Queries;

/// <summary>
/// 查询应用详细信息.
/// </summary>
public class QueryChatAppConfigCommand : IRequest<QueryChatAppConfigCommandResponse>
{
    /// <summary>
    /// 团队 id.
    /// </summary>
    public int TeamId { get; init; }

    /// <summary>
    /// 应用id.
    /// </summary>
    public Guid AppId { get; init; }
}

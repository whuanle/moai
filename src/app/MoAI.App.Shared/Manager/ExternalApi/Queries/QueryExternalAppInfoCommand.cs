using MediatR;
using MoAI.App.Manager.ExternalApi.Models;
using MoAI.Infra.Models;

namespace MoAI.App.Manager.ExternalApi.Queries;

/// <summary>
/// 查询系统接入信息.
/// </summary>
public class QueryExternalAppInfoCommand : IUserIdContext, IRequest<QueryExternalAppInfoCommandResponse?>
{
    /// <inheritdoc/>
    public int ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }

    /// <summary>
    /// 团队id.
    /// </summary>
    public int TeamId { get; init; }
}

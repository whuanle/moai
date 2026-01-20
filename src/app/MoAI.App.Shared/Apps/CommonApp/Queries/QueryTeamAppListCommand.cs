using MediatR;
using MoAI.App.Apps.CommonApp.Responses;
using MoAI.App.Models;
using MoAI.Infra.Models;

namespace MoAI.App.Apps.CommonApp.Queries;

/// <summary>
/// 查询团队内部可用的应用列表.
/// </summary>
public class QueryTeamAppListCommand : IRequest<QueryTeamAppListCommandResponse>
{
    /// <summary>
    /// 团队 id.
    /// </summary>
    public int TeamId { get; init; }

    /// <summary>
    /// 应用类型.
    /// </summary>
    public AppType? AppType { get; init; }

    /// <summary>
    /// 分类id.
    /// </summary>
    public int? ClassifyId { get; init; }

    /// <summary>
    /// 是否外部应用.
    /// </summary>
    public bool? IsForeign { get; init; }
}

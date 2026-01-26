using MediatR;
using MoAI.App.Manager.Models;
using MoAI.Infra.Models;

namespace MoAI.App.AppStore.Queries;

/// <summary>
/// 查询应用简单信息.
/// </summary>
public class QueryOneAppSimpleInfoCommand : IRequest<QueryAppListCommandResponseItem>
{
    /// <summary>
    /// 应用id.
    /// </summary>
    public Guid AppId { get; init; }
}

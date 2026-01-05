using MediatR;
using MoAI.Plugin.Authorization.Queries.Responses;

namespace MoAI.Plugin.Authorization.Queries;

/// <summary>
/// 查询所有插件及其授权的团队列表.
/// </summary>
public class QueryPluginAuthorizationsCommand : IRequest<QueryPluginAuthorizationsCommandResponse>
{
}

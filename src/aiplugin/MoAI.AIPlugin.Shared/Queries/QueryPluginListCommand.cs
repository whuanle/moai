
using MediatR;
using MoAI.AIPlugin.Queries.Responses;

namespace MoAI.AIPlugin.Queries;

/// <summary>
/// 查询全部可用插件列表.
/// </summary>
public class QueryPluginListCommand : IRequest<QueryPluginListCommandResponse>
{
}

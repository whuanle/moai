using MediatR;
using MoAI.AiPlugin.Queries.Responses;

namespace MoAI.AiPlugin.Queries;

/// <summary>
/// 查询全部可用插件列表.
/// </summary>
public class QueryPluginListCommand : IRequest<QueryPluginListCommandResponse>
{
}

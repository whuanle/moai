using MediatR;
using MoAI.Plugin.Queries.Responses;

namespace MoAI.Plugin.Queries;

/// <summary>
/// 查询插件分类列表.
/// </summary>
public class QueryPluginClassifyListCommand : IRequest<QueryPluginClassifyListCommandResponse>
{
}

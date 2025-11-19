using MediatR;
using MoAI.Plugin.Classify.Queries.Responses;

namespace MoAI.Plugin.Classify.Queries;

/// <summary>
/// 查询插件分类列表.
/// </summary>
public class QueryPluginClassifyListCommand : IRequest<QueryPluginClassifyListCommandResponse>
{
}

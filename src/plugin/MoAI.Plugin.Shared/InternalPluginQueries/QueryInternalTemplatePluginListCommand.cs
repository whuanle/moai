using MediatR;
using MoAI.Plugin.Queries.Responses;

namespace MoAI.Plugin.InternalQueries;

/// <summary>
/// 查询内置模板插件列表.
/// </summary>
public class QueryInternalTemplatePluginListCommand : IRequest<QueryInternalTemplatePluginListCommandResponse>
{
}
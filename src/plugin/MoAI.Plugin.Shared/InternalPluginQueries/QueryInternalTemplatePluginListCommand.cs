using MediatR;
using MoAI.Plugin.Models;
using MoAI.Plugin.Queries.Responses;

namespace MoAI.Plugin.InternalQueries;

/// <summary>
/// 查询内置插件模板列表.
/// </summary>
public class QueryInternalTemplatePluginListCommand : IRequest<QueryInternalTemplatePluginListCommandResponse>
{
    /// <summary>
    /// 分类筛选.
    /// </summary>
    public InternalPluginClassify? Classify { get; init; }
}
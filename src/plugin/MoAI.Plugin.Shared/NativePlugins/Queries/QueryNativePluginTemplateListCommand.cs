using MediatR;
using MoAI.Plugin.Models;
using MoAI.Plugin.NativePlugins.Queries.Responses;

namespace MoAI.Plugin.NativePlugins.Queries;

/// <summary>
/// 查询内置插件模板列表.
/// </summary>
public class QueryNativePluginTemplateListCommand : IRequest<QueryInternalTemplatePluginListCommandResponse>
{
    /// <summary>
    /// 分类筛选.
    /// </summary>
    public NativePluginClassify? Classify { get; init; }
}
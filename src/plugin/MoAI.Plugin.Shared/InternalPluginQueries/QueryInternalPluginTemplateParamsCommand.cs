using MediatR;
using MoAI.Plugin.Queries.Responses;

namespace MoAI.Plugin.InternalQueries;

/// <summary>
/// 查询内置插件模板参数.
/// </summary>
public class QueryInternalPluginTemplateParamsCommand : IRequest<QueryInternalPluginTemplateParamsCommandResponse>
{
    /// <summary>
    /// 插件键.
    /// </summary>
    public string TemplatePluginKey { get; init; } = string.Empty;
}

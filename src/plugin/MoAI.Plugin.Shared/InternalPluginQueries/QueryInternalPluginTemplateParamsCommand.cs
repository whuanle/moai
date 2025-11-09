using MediatR;
using MoAI.Infra.Models;
using MoAI.Plugin.Queries.Responses;

namespace MoAI.Plugin.InternalQueries;

/// <summary>
/// 获取一个内置插件模板需要的配置参数和运行参数.
/// </summary>
public class QueryInternalPluginTemplateParamsCommand : IRequest<QueryInternalPluginTemplateParamsCommandResponse>
{
    /// <summary>
    /// 插件键.
    /// </summary>
    public string TemplatePluginKey { get; init; } = string.Empty;
}

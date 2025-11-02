#pragma warning disable CA1810 // 以内联方式初始化引用类型的静态字段

using MediatR;
using MoAI.Infra.Exceptions;
using MoAI.Plugin.InternalQueries;
using MoAI.Plugin.Plugins;
using MoAI.Plugin.Queries.Responses;

namespace MoAI.Plugin.Queries;

/// <summary>
/// <inheritdoc cref="QueryInternalPluginTemplateParamsCommand"/>
/// </summary>
public class QueryInternalPluginTemplateParamsCommandHandler : IRequestHandler<QueryInternalPluginTemplateParamsCommand, QueryInternalPluginTemplateParamsCommandResponse>
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryInternalPluginTemplateParamsCommandHandler"/> class.
    /// </summary>
    /// <param name="serviceProvider"></param>
    public QueryInternalPluginTemplateParamsCommandHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    public async Task<QueryInternalPluginTemplateParamsCommandResponse> Handle(QueryInternalPluginTemplateParamsCommand request, CancellationToken cancellationToken)
    {
        if (!InternalPluginFactory.Plugins.TryGetValue(request.TemplatePluginKey, out var pluginInfo))
        {
            throw new BusinessException("未找到插件模板") { StatusCode = 404 };
        }

        var service = _serviceProvider.GetService(pluginInfo.Type);
        if (service is null)
        {
            throw new BusinessException("未找到插件模板") { StatusCode = 404 };
        }

        var plugin = service as IInternalPluginRuntime;

        return new QueryInternalPluginTemplateParamsCommandResponse
        {
            Items = await plugin!.ExportConfigAsync()
        };
    }
}

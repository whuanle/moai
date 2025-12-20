#pragma warning disable CA1810 // 以内联方式初始化引用类型的静态字段

using MediatR;
using MoAI.Infra.Exceptions;
using MoAI.Plugin.NativePlugins.Queries;
using MoAI.Plugin.NativePlugins.Queries.Responses;

namespace MoAI.Plugin.Queries;

/// <summary>
/// <inheritdoc cref="QueryNativePluginTemplateParamsCommand"/>
/// </summary>
public class QueryNativePluginTemplateParamsCommandHandler : IRequestHandler<QueryNativePluginTemplateParamsCommand, QueryNativePluginTemplateParamsCommandResponse>
{
    private readonly INativePluginFactory _nativePluginFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryNativePluginTemplateParamsCommandHandler"/> class.
    /// </summary>
    /// <param name="nativePluginFactory"></param>
    public QueryNativePluginTemplateParamsCommandHandler(INativePluginFactory nativePluginFactory)
    {
        _nativePluginFactory = nativePluginFactory;
    }

    /// <inheritdoc/>
    public async Task<QueryNativePluginTemplateParamsCommandResponse> Handle(QueryNativePluginTemplateParamsCommand request, CancellationToken cancellationToken)
    {
        var pluginInfo = _nativePluginFactory.GetPluginByKey(request.TemplatePluginKey);
        if (pluginInfo == null)
        {
            throw new BusinessException("未找到插件模板") { StatusCode = 404 };
        }

        return new QueryNativePluginTemplateParamsCommandResponse
        {
            Description = pluginInfo.Description,
            Classify = pluginInfo.Classify,
            ExampleValue = pluginInfo.ExampleValue,
            FieldTemplates = pluginInfo.FieldTemplates,
            Key = pluginInfo.Key,
            Name = pluginInfo.Name,
            PluginType = pluginInfo.PluginType,
        };
    }
}

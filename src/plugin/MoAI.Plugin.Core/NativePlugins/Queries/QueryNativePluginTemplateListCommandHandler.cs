#pragma warning disable CA1810 // 以内联方式初始化引用类型的静态字段

using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Models;
using MoAI.Plugin.NativePlugins.Queries;
using MoAI.Plugin.NativePlugins.Queries.Responses;

namespace MoAI.Plugin.Queries;

/// <summary>
/// <inheritdoc cref="QueryNativePluginTemplateListCommand"/>
/// </summary>
public class QueryNativePluginTemplateListCommandHandler : IRequestHandler<QueryNativePluginTemplateListCommand, QueryNativePluginTemplateListCommandResponse>
{
    private readonly INativePluginFactory _nativePluginFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryNativePluginTemplateListCommandHandler"/> class.
    /// </summary>
    /// <param name="nativePluginFactory"></param>
    public QueryNativePluginTemplateListCommandHandler(INativePluginFactory nativePluginFactory)
    {
        _nativePluginFactory = nativePluginFactory;
    }

    /// <inheritdoc/>
    public async Task<QueryNativePluginTemplateListCommandResponse> Handle(QueryNativePluginTemplateListCommand request, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        var pluginTemplates = _nativePluginFactory.GetPlugins();

        NativePluginTemplateInfo[] plugins;

        if (request.Classify != null)
        {
            plugins = pluginTemplates.Where(x => x.Classify == request.Classify).ToArray();
        }
        else
        {
            plugins = pluginTemplates.ToArray();
        }

        return new QueryNativePluginTemplateListCommandResponse
        {
            Plugins = plugins,
            ClassifyCount = pluginTemplates.GroupBy(x => x.Classify).Select(x => new KeyValue<string, int>
            {
                Key = x.Key.ToString(),
                Value = x.Count()
            }).ToArray()
        };
    }
}

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
public class QueryNativePluginTemplateListCommandHandler : IRequestHandler<QueryNativePluginTemplateListCommand, QueryInternalTemplatePluginListCommandResponse>
{
    /// <inheritdoc/>
    public async Task<QueryInternalTemplatePluginListCommandResponse> Handle(QueryNativePluginTemplateListCommand request, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        NativePluginTemplateInfo[] plugins;

        if (request.Classify != null)
        {
            plugins = NativePluginFactory.Plugins.Where(x => x.Classify == request.Classify).ToArray();
        }
        else
        {
            plugins = NativePluginFactory.Plugins.ToArray();
        }

        return new QueryInternalTemplatePluginListCommandResponse
        {
            Plugins = plugins,
            ClassifyCount = NativePluginFactory.Plugins.GroupBy(x => x.Classify).Select(x => new KeyValue<string, int>
            {
                Key = x.Key.ToString(),
                Value = x.Count()
            }).ToArray()
        };
    }
}

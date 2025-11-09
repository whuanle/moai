#pragma warning disable CA1810 // 以内联方式初始化引用类型的静态字段

using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Models;
using MoAI.Plugin.InternalQueries;
using MoAI.Plugin.Queries.Responses;

namespace MoAI.Plugin.Queries;

/// <summary>
/// <inheritdoc cref="QueryInternalTemplatePluginListCommand"/>
/// </summary>
public class QueryInternalTemplatePluginListCommandHandler : IRequestHandler<QueryInternalTemplatePluginListCommand, QueryInternalTemplatePluginListCommandResponse>
{
    /// <inheritdoc/>
    public async Task<QueryInternalTemplatePluginListCommandResponse> Handle(QueryInternalTemplatePluginListCommand request, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        InternalTemplatePlugin[] plugins;

        if (request.Classify != null)
        {
            plugins = InternalPluginFactory.Plugins.Where(x => x.Classify == request.Classify).ToArray();
        }
        else
        {
            plugins = InternalPluginFactory.Plugins.ToArray();
        }

        return new QueryInternalTemplatePluginListCommandResponse
        {
            Plugins = plugins,
            ClassifyCount = InternalPluginFactory.Plugins.GroupBy(x => x.Classify).Select(x => new KeyValue<string, int>
            {
                Key = x.Key.ToString(),
                Value = x.Count()
            }).ToArray()
        };
    }
}

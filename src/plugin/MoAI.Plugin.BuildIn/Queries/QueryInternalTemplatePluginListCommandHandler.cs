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
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryInternalTemplatePluginListCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public QueryInternalTemplatePluginListCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QueryInternalTemplatePluginListCommandResponse> Handle(QueryInternalTemplatePluginListCommand request, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        InternalTemplatePlugin[] plugins;

        if (request.Classify != null)
        {
            plugins = InternalPluginFactory.Plugins.Where(x => x.Value.Classify == request.Classify).Select(x => new InternalTemplatePlugin
            {
                Description = x.Value.Description,
                TemplatePluginKey = x.Value.PluginKey,
                PluginName = x.Value.PluginName,
                Classify = x.Value.Classify
            }).ToArray();
        }
        else
        {
            plugins = InternalPluginFactory.Plugins.Select(x => new InternalTemplatePlugin
            {
                Description = x.Value.Description,
                TemplatePluginKey = x.Value.PluginKey,
                PluginName = x.Value.PluginName,
                Classify = x.Value.Classify
            }).ToArray();
        }

        return new QueryInternalTemplatePluginListCommandResponse
        {
            Plugins = plugins,
            ClassifyCount = InternalPluginFactory.Plugins.Values.GroupBy(x => x.Classify).Select(x => new KeyValue<string, int>
            {
                Key = x.Key.ToString(),
                Value = x.Count()
            }).ToArray()
        };
    }
}

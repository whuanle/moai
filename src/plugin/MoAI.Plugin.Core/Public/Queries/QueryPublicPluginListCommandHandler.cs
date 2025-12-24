using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AI.Models;
using MoAI.Database;
using MoAI.Plugin.Models;

namespace MoAI.Plugin.Public.Queries;

/// <summary>
/// <inheritdoc cref="QueryPublicPluginListCommand"/>
/// </summary>
public class QueryPublicPluginListCommandHandler : IRequestHandler<QueryPublicPluginListCommand, QueryPublicPluginListCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly INativePluginFactory _nativePluginFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryPublicPluginListCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="nativePluginFactory"></param>
    public QueryPublicPluginListCommandHandler(DatabaseContext databaseContext, INativePluginFactory nativePluginFactory)
    {
        _databaseContext = databaseContext;
        _nativePluginFactory = nativePluginFactory;
    }

    /// <inheritdoc/>
    public async Task<QueryPublicPluginListCommandResponse> Handle(QueryPublicPluginListCommand request, CancellationToken cancellationToken)
    {
        var toolPluginTemplates = _nativePluginFactory.GetPlugins().Where(x => x.PluginType == PluginType.ToolPlugin).ToArray();
        var plugins = await _databaseContext.Plugins.Where(x => x.IsPublic)
            .Select(x => new PluginSimpleInfo
            {
                PluginName = x.PluginName,
                Title = x.Title,
                Description = x.Description,
                ClassifyId = x.ClassifyId
            }).ToListAsync();

        foreach (var item in toolPluginTemplates)
        {
            var toolPlugin = plugins.FirstOrDefault(x => x.PluginName == item.Key);
            if (toolPlugin == null)
            {
                plugins.Add(new PluginSimpleInfo
                {
                    Description = item.Description,
                    Title = item.Name,
                    PluginName = item.Key,
                    ClassifyId = 0
                });
            }
        }

        return new QueryPublicPluginListCommandResponse
        {
            Items = plugins
        };
    }
}

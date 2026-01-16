using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AI.Models;
using MoAI.Database;
using MoAI.Plugin.Models;
using MoAI.Plugin.TeamPlugins.Queries.Responses;

namespace MoAI.Plugin.TeamPlugins.Queries;

/// <summary>
/// <inheritdoc cref="QueryUserViewPluginListCommand"/>
/// </summary>
public class QueryUserViewPluginListCommandHandler : IRequestHandler<QueryUserViewPluginListCommand, QueryUserViewPluginListCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly INativePluginFactory _nativePluginFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryUserViewPluginListCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="nativePluginFactory"></param>
    public QueryUserViewPluginListCommandHandler(DatabaseContext databaseContext, INativePluginFactory nativePluginFactory)
    {
        _databaseContext = databaseContext;
        _nativePluginFactory = nativePluginFactory;
    }

    /// <inheritdoc/>
    public async Task<QueryUserViewPluginListCommandResponse> Handle(QueryUserViewPluginListCommand request, CancellationToken cancellationToken)
    {
        // 查询公开插件和团队专属插件
        var plugins = await _databaseContext.Plugins
            .Where(x => x.IsPublic || x.TeamId == request.TeamId)
            .Select(x => new PluginSimpleInfo
            {
                PluginName = x.PluginName,
                Title = x.Title,
                Description = x.Description,
                ClassifyId = x.ClassifyId
            })
            .ToListAsync(cancellationToken);

        // 按 PluginName 去重（优先保留已存在的）
        var distinctPlugins = plugins
            .GroupBy(x => x.PluginName)
            .Select(g => g.First())
            .ToList();

        // 添加原生工具插件
        var toolPluginTemplates = _nativePluginFactory.GetPlugins()
            .Where(x => x.PluginType == PluginType.ToolPlugin)
            .ToArray();

        foreach (var item in toolPluginTemplates)
        {
            var toolPlugin = distinctPlugins.FirstOrDefault(x => x.PluginName == item.Key);
            if (toolPlugin == null)
            {
                distinctPlugins.Add(new PluginSimpleInfo
                {
                    Description = item.Description,
                    Title = item.Name,
                    PluginName = item.Key,
                    ClassifyId = 0
                });
            }
        }

        return new QueryUserViewPluginListCommandResponse
        {
            Items = distinctPlugins
        };
    }
}

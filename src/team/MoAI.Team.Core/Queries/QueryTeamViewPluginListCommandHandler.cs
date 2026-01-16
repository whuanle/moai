using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AI.Models;
using MoAI.Database;
using MoAI.Plugin;
using MoAI.Plugin.Models;
using MoAI.Team.Queries.Responses;

namespace MoAI.Team.Queries;

/// <summary>
/// <inheritdoc cref="QueryTeamViewPluginListCommand"/>
/// </summary>
public class QueryTeamViewPluginListCommandHandler : IRequestHandler<QueryTeamViewPluginListCommand, QueryTeamViewPluginListCommandResponse>
{
    private readonly DatabaseContext _dbContext;
    private readonly INativePluginFactory _nativePluginFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryTeamViewPluginListCommandHandler"/> class.
    /// </summary>
    /// <param name="dbContext">数据库上下文.</param>
    /// <param name="nativePluginFactory">原生插件工厂.</param>
    public QueryTeamViewPluginListCommandHandler(DatabaseContext dbContext, INativePluginFactory nativePluginFactory)
    {
        _dbContext = dbContext;
        _nativePluginFactory = nativePluginFactory;
    }

    /// <inheritdoc/>
    public async Task<QueryTeamViewPluginListCommandResponse> Handle(QueryTeamViewPluginListCommand request, CancellationToken cancellationToken)
    {
        // 查询团队被授权的插件 ID
        var authorizedPluginIds = _dbContext.PluginAuthorizations
            .Where(a => a.TeamId == request.TeamId)
            .Select(a => a.PluginId);

        // 查询公开插件、团队专属插件和团队被授权的插件
        var plugins = await _dbContext.Plugins
            .Where(x => x.IsPublic || x.TeamId == request.TeamId || authorizedPluginIds.Contains(x.Id))
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

        return new QueryTeamViewPluginListCommandResponse
        {
            Plugins = distinctPlugins
        };
    }
}

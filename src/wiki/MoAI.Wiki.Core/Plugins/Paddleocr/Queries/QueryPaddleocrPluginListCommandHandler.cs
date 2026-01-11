using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Wiki.Plugins.Paddleocr.Models;
using MoAI.Wiki.Plugins.Paddleocr.Queries;
using MoAI.Wiki.Plugins.Paddleocr.Queries.Responses;

namespace MoAI.Wiki.Plugins.Paddleocr;

/// <summary>
/// <inheritdoc cref="QueryPaddleocrPluginListCommand"/>
/// </summary>
public class QueryPaddleocrPluginListCommandHandler : IRequestHandler<QueryPaddleocrPluginListCommand, QueryPaddleocrPluginListCommandResponse>
{
    private static readonly string[] PaddleocrTemplateKeys = new[]
    {
        "paddleocr_ocr",
        "paddleocr_vl",
        "paddleocr_structure_v3"
    };

    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryPaddleocrPluginListCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public QueryPaddleocrPluginListCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QueryPaddleocrPluginListCommandResponse> Handle(QueryPaddleocrPluginListCommand request, CancellationToken cancellationToken)
    {
        var teamId = await _databaseContext.Wikis.Where(x => x.Id == request.WikiId).Select(x => x.TeamId).FirstOrDefaultAsync(cancellationToken);

        // 查询公开或被授权的 Paddleocr 插件
        // 条件：
        // 1. 插件模板 key 是 paddleocr_ocr、paddleocr_vl、paddleocr_structure_v3
        // 2. 插件是公开的，或者插件被授权给用户所属的团队
        var plugins = await (
            from plugin in _databaseContext.Plugins
            join native in _databaseContext.PluginNatives on plugin.PluginId equals native.Id
            where PaddleocrTemplateKeys.Contains(native.TemplatePluginKey)
                && (plugin.IsPublic
                    || _databaseContext.PluginAuthorizations
                        .Any(auth => auth.PluginId == plugin.Id && auth.TeamId == teamId))
            select new PaddleocrPluginItem
            {
                PluginId = plugin.Id,
                PluginName = plugin.PluginName,
                Title = plugin.Title,
                Description = plugin.Description,
                TemplatePluginKey = native.TemplatePluginKey
            }).ToListAsync(cancellationToken);

        return new QueryPaddleocrPluginListCommandResponse
        {
            Items = plugins
        };
    }
}

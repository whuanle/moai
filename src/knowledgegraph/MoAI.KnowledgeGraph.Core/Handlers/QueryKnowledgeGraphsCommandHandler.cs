using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.KnowledgeGraph.Queries;
using MoAI.KnowledgeGraph.Queries.Responses;
using MoAI.KnowledgeGraph.Services;
using MoAI.Settings.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="QueryKnowledgeGraphsCommand"/>
/// </summary>
public class QueryKnowledgeGraphsCommandHandler : IRequestHandler<QueryKnowledgeGraphsCommand, QueryKnowledgeGraphsCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IKnowledgeGraphAuthorizer _authorizer;
    private readonly IKnowledgeGraphSettingsService _settingsService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryKnowledgeGraphsCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="authorizer">权限判定.</param>
    /// <param name="settingsService">知识图谱设置.</param>
    public QueryKnowledgeGraphsCommandHandler(DatabaseContext databaseContext, IKnowledgeGraphAuthorizer authorizer, IKnowledgeGraphSettingsService settingsService)
    {
        _databaseContext = databaseContext;
        _authorizer = authorizer;
        _settingsService = settingsService;
    }

    /// <inheritdoc/>
    public async Task<QueryKnowledgeGraphsCommandResponse> Handle(QueryKnowledgeGraphsCommand request, CancellationToken cancellationToken)
    {
        var role = await _authorizer.RequireTeamRoleAsync(request.TeamId, adminOnly: false, cancellationToken);
        var settings = await _settingsService.GetAsync(cancellationToken);

        var items = await _databaseContext.KnowledgeGraphs
            .Where(x => x.TeamId == request.TeamId)
            .OrderBy(x => x.Id)
            .Select(x => new KnowledgeGraphItem
            {
                KgId = x.Id,
                TeamId = x.TeamId,
                Name = x.Name,
                Description = x.Description,
                TemplateKey = x.TemplateKey,
                CreateTime = x.CreateTime,
            })
            .ToListAsync(cancellationToken);

        return new QueryKnowledgeGraphsCommandResponse
        {
            TeamId = request.TeamId,
            MyRole = (int)role,
            Enabled = settings.Enabled,
            Items = items,
        };
    }
}

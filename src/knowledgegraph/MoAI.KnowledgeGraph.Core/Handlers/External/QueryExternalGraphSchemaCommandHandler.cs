using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.KnowledgeGraph.External;
using MoAI.KnowledgeGraph.Queries.Responses;
using MoAI.KnowledgeGraph.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="QueryExternalGraphSchemaCommand"/>
/// </summary>
public class QueryExternalGraphSchemaCommandHandler : IRequestHandler<QueryExternalGraphSchemaCommand, QueryExternalGraphSchemaCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IExternalKnowledgeGraphAuthorizer _externalAuthorizer;
    private readonly IKnowledgeGraphStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryExternalGraphSchemaCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="externalAuthorizer">外部授权器.</param>
    /// <param name="store">图存储.</param>
    public QueryExternalGraphSchemaCommandHandler(DatabaseContext databaseContext, IExternalKnowledgeGraphAuthorizer externalAuthorizer, IKnowledgeGraphStore store)
    {
        _databaseContext = databaseContext;
        _externalAuthorizer = externalAuthorizer;
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<QueryExternalGraphSchemaCommandResponse> Handle(QueryExternalGraphSchemaCommand request, CancellationToken cancellationToken)
    {
        await _externalAuthorizer.AuthorizeAsync(request.KnowledgeGraphId, request.Caller.TeamId, write: false, cancellationToken);
        // 内部 schema 查询无系统设置门禁，外部版保持同语义；managed 图 schema 直接读 PG 类型表并附带图库计数.
        var entityTypeEntities = await _databaseContext.KnowledgeGraphEntityTypes
            .Where(x => x.KnowledgeGraphId == request.KnowledgeGraphId)
            .OrderBy(x => x.Sort).ThenBy(x => x.Id)
            .Select(x => new { x.Id, x.Name, x.Color, x.Description, x.Properties })
            .ToListAsync(cancellationToken);

        var entityTypes = new List<KnowledgeGraphEntityTypeItem>();
        foreach (var x in entityTypeEntities)
        {
            var count = await _store.CountNodesByEntityTypeAsync(request.KnowledgeGraphId, x.Id, cancellationToken);
            entityTypes.Add(new KnowledgeGraphEntityTypeItem
            {
                EntityTypeId = x.Id,
                Name = x.Name,
                Color = x.Color,
                Description = x.Description,
                Count = count,
                Properties = KnowledgeGraphPropertyJson.ParseDefinitions(x.Properties),
            });
        }

        var relationTypeEntities = await _databaseContext.KnowledgeGraphRelationTypes
            .Where(x => x.KnowledgeGraphId == request.KnowledgeGraphId)
            .OrderBy(x => x.Sort).ThenBy(x => x.Id)
            .Select(x => new { x.Id, x.Name, x.Color, x.Description, x.SourceTypeId, x.TargetTypeId })
            .ToListAsync(cancellationToken);

        var relationTypes = new List<KnowledgeGraphRelationTypeItem>();
        foreach (var x in relationTypeEntities)
        {
            var count = await _store.CountEdgesByRelationTypeAsync(request.KnowledgeGraphId, x.Id, cancellationToken);
            relationTypes.Add(new KnowledgeGraphRelationTypeItem
            {
                RelationTypeId = x.Id,
                Name = x.Name,
                Color = x.Color,
                Description = x.Description,
                SourceTypeId = x.SourceTypeId,
                TargetTypeId = x.TargetTypeId,
                Count = count,
            });
        }

        return new QueryExternalGraphSchemaCommandResponse
        {
            EntityTypes = entityTypes,
            RelationTypes = relationTypes,
        };
    }
}

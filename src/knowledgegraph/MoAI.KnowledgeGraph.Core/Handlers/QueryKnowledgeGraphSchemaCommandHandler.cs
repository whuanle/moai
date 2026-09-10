using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.KnowledgeGraph.Queries;
using MoAI.KnowledgeGraph.Queries.Responses;
using MoAI.KnowledgeGraph.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="QueryKnowledgeGraphSchemaCommand"/>
/// </summary>
public class QueryKnowledgeGraphSchemaCommandHandler : IRequestHandler<QueryKnowledgeGraphSchemaCommand, QueryKnowledgeGraphSchemaCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IKnowledgeGraphAuthorizer _authorizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryKnowledgeGraphSchemaCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="authorizer">权限判定.</param>
    public QueryKnowledgeGraphSchemaCommandHandler(DatabaseContext databaseContext, IKnowledgeGraphAuthorizer authorizer)
    {
        _databaseContext = databaseContext;
        _authorizer = authorizer;
    }

    /// <inheritdoc/>
    public async Task<QueryKnowledgeGraphSchemaCommandResponse> Handle(QueryKnowledgeGraphSchemaCommand request, CancellationToken cancellationToken)
    {
        await _authorizer.AuthorizeAsync(request.KgId, adminOnly: false, cancellationToken);

        var entityTypes = await _databaseContext.KnowledgeGraphEntityTypes
            .Where(x => x.KgId == request.KgId)
            .OrderBy(x => x.Sort).ThenBy(x => x.Id)
            .Select(x => new KnowledgeGraphEntityTypeItem
            {
                EntityTypeId = x.Id,
                Name = x.Name,
                Color = x.Color,
                Description = x.Description,
            })
            .ToListAsync(cancellationToken);

        var relationTypes = await _databaseContext.KnowledgeGraphRelationTypes
            .Where(x => x.KgId == request.KgId)
            .OrderBy(x => x.Sort).ThenBy(x => x.Id)
            .Select(x => new KnowledgeGraphRelationTypeItem
            {
                RelationTypeId = x.Id,
                Name = x.Name,
                Color = x.Color,
                Description = x.Description,
                SourceTypeId = x.SourceTypeId,
                TargetTypeId = x.TargetTypeId,
            })
            .ToListAsync(cancellationToken);

        return new QueryKnowledgeGraphSchemaCommandResponse { EntityTypes = entityTypes, RelationTypes = relationTypes };
    }
}

using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.KnowledgeGraph.Models;
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
    private readonly IKnowledgeGraphStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryKnowledgeGraphSchemaCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="authorizer">权限判定.</param>
    /// <param name="store">图存储.</param>
    public QueryKnowledgeGraphSchemaCommandHandler(DatabaseContext databaseContext, IKnowledgeGraphAuthorizer authorizer, IKnowledgeGraphStore store)
    {
        _databaseContext = databaseContext;
        _authorizer = authorizer;
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<QueryKnowledgeGraphSchemaCommandResponse> Handle(QueryKnowledgeGraphSchemaCommand request, CancellationToken cancellationToken)
    {
        var (graph, _) = await _authorizer.AuthorizeAsync(request.KnowledgeGraphId, adminOnly: false, cancellationToken);

        if (string.Equals(graph.Mode, KnowledgeGraphModes.Connected, StringComparison.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(graph.Database))
            {
                throw new BusinessException("接入图谱缺少数据库配置.") { StatusCode = 409 };
            }

            var introspection = await _store.IntrospectAsync(graph.Database, cancellationToken);
            return new QueryKnowledgeGraphSchemaCommandResponse
            {
                Mode = KnowledgeGraphModes.Connected,
                Database = graph.Database,
                ReadOnly = true,
                EntityTypes = introspection.Labels.Select(x => new KnowledgeGraphEntityTypeItem { EntityTypeId = null, Name = x.Name, Color = string.Empty, Description = string.Empty, Count = x.Count }).ToList(),
                RelationTypes = introspection.RelationshipTypes.Select(x => new KnowledgeGraphRelationTypeItem { RelationTypeId = null, Name = x.Name, Color = string.Empty, Description = string.Empty, Count = x.Count }).ToList(),
                PropertyKeys = introspection.PropertyKeys.ToList(),
            };
        }

        var entityTypes = await _databaseContext.KnowledgeGraphEntityTypes
            .Where(x => x.KnowledgeGraphId == request.KnowledgeGraphId)
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
            .Where(x => x.KnowledgeGraphId == request.KnowledgeGraphId)
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

        return new QueryKnowledgeGraphSchemaCommandResponse
        {
            EntityTypes = entityTypes,
            RelationTypes = relationTypes,
            Mode = KnowledgeGraphModes.Managed,
            Database = graph.Database,
            ReadOnly = false,
        };
    }
}

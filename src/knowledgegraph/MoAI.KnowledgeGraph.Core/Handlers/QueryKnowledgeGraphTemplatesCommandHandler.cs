using MediatR;
using MoAI.KnowledgeGraph.Models;
using MoAI.KnowledgeGraph.Queries;
using MoAI.KnowledgeGraph.Queries.Responses;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="QueryKnowledgeGraphTemplatesCommand"/>
/// </summary>
public class QueryKnowledgeGraphTemplatesCommandHandler : IRequestHandler<QueryKnowledgeGraphTemplatesCommand, QueryKnowledgeGraphTemplatesCommandResponse>
{
    /// <inheritdoc/>
    public Task<QueryKnowledgeGraphTemplatesCommandResponse> Handle(QueryKnowledgeGraphTemplatesCommand request, CancellationToken cancellationToken)
    {
        var items = KnowledgeGraphTemplates.All
            .Select(x => new KnowledgeGraphTemplateItem
            {
                Key = x.Key,
                Name = x.Name,
                Description = x.Description,
                EntityTypes = x.EntityTypes.ToList(),
                RelationTypes = x.RelationTypes.Select(r => r.Name).ToList(),
            })
            .ToList();

        return Task.FromResult(new QueryKnowledgeGraphTemplatesCommandResponse { Items = items });
    }
}

using MediatR;
using MoAI.AI.Models;
using MoAI.App.Queries;
using MoAI.App.Queries.Responses;

namespace MoAI.App.Handlers;

/// <summary>
/// <inheritdoc cref="QueryAgentMemoryDefaultsCommand"/>
/// </summary>
public class QueryAgentMemoryDefaultsCommandHandler : IRequestHandler<QueryAgentMemoryDefaultsCommand, QueryAgentMemoryDefaultsResponse>
{
    /// <inheritdoc/>
    public Task<QueryAgentMemoryDefaultsResponse> Handle(QueryAgentMemoryDefaultsCommand request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new QueryAgentMemoryDefaultsResponse
        {
            Prompt = AgentMemorySettings.DefaultPrompt
        });
    }
}

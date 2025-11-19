using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Prompt.Queries.Responses;

namespace MoAI.Prompt.Queries;

/// <summary>
/// <inheritdoc cref="QueryePromptClassCommand"/>
/// </summary>
public class QueryePromptClassCommandHandler : IRequestHandler<QueryePromptClassCommand, QueryePromptClassCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryePromptClassCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public QueryePromptClassCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QueryePromptClassCommandResponse> Handle(QueryePromptClassCommand request, CancellationToken cancellationToken)
    {
        var classifies = await _databaseContext.Classifies.Where(x => x.Type == "prompt")
            .Select(x => new PromptClassifyItem
            {
                ClassifyId = x.Id,
                Name = x.Name,
                Description = x.Description
            })
            .ToArrayAsync();

        return new QueryePromptClassCommandResponse { Items = classifies };
    }
}

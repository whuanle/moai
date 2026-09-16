using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Skill.Queries;
using MoAI.Skill.Queries.Responses;

namespace MoAI.Skill.Handlers;

/// <summary>
/// <inheritdoc cref="QuerySkillOptionsCommand"/>
/// </summary>
public class QuerySkillOptionsCommandHandler : IRequestHandler<QuerySkillOptionsCommand, QuerySkillOptionsCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuerySkillOptionsCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public QuerySkillOptionsCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QuerySkillOptionsCommandResponse> Handle(QuerySkillOptionsCommand request, CancellationToken cancellationToken)
    {
        var items = await _databaseContext.Skills.AsNoTracking()
            .Where(x => !x.IsDisable)
            .Where(x => x.IsSystem
                || x.IsPublic
                || (request.TeamId > 0 && x.TeamId == request.TeamId)
                || (request.IncludePersonal && x.TeamId == 0 && !x.IsSystem && x.CreateUserId == request.ContextUserId))
            .OrderBy(x => x.Key)
            .Select(x => new SkillOptionItem
            {
                Id = x.Id,
                Key = x.Key,
                Name = x.Name,
                Description = x.Description,
                IsSystem = x.IsSystem,
                TeamId = x.TeamId,
            })
            .ToListAsync(cancellationToken);

        return new QuerySkillOptionsCommandResponse { Items = items };
    }
}

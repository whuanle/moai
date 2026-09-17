using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Skill.Queries;
using MoAI.Skill.Queries.Responses;
using MoAI.Skill.Services;

namespace MoAI.Skill.Handlers;

/// <summary>
/// <inheritdoc cref="QuerySkillsCommand"/>
/// </summary>
public class QuerySkillsCommandHandler : IRequestHandler<QuerySkillsCommand, QuerySkillsCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuerySkillsCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public QuerySkillsCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QuerySkillsCommandResponse> Handle(QuerySkillsCommand request, CancellationToken cancellationToken)
    {
        var query = _databaseContext.Skills.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.SearchText))
        {
            var keyword = request.SearchText.Trim();
            query = query.Where(x => x.Key.Contains(keyword) || x.Name.Contains(keyword) || x.Description.Contains(keyword));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var skills = await query
            .OrderByDescending(x => x.IsSystem)
            .ThenBy(x => x.Key)
            .Skip((request.PageNo - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var items = skills.Select(x => new SkillListItem
        {
            Id = x.Id,
            Key = x.Key,
            Name = x.Name,
            Description = x.Description,
            IsSystem = x.IsSystem,
            IsDisable = x.IsDisable,
            TeamId = x.TeamId,
            IsPublic = x.IsPublic,
            FileCount = x.IsSystem
                ? BuiltinSkills.GetFiles(x.Key).Count
                : SkillService.ParseFiles(x.Files).Count,
            CreateUserId = (int)x.CreateUserId,
            CreateTime = x.CreateTime,
            UpdateUserId = (int)x.UpdateUserId,
            UpdateTime = x.UpdateTime,
        }).ToList();

        return new QuerySkillsCommandResponse
        {
            TotalCount = totalCount,
            Items = items,
        };
    }
}

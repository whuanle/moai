using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Skill.Queries;
using MoAI.Skill.Queries.Responses;
using MoAI.Skill.Services;

namespace MoAI.Skill.Handlers;

/// <summary>
/// <inheritdoc cref="QuerySkillCommand"/>
/// </summary>
public class QuerySkillCommandHandler : IRequestHandler<QuerySkillCommand, QuerySkillCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuerySkillCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public QuerySkillCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QuerySkillCommandResponse> Handle(QuerySkillCommand request, CancellationToken cancellationToken)
    {
        var skill = await _databaseContext.Skills.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.SkillId, cancellationToken)
            ?? throw new BusinessException("技能不存在.") { StatusCode = 404 };

        var files = skill.IsSystem
            ? BuiltinSkills.GetFiles(skill.Key)
                .Select(p => new Models.SkillFileItem { Path = p, FileId = 0, FileName = Path.GetFileName(p) })
                .ToList()
            : SkillService.ParseFiles(skill.Files);

        return new QuerySkillCommandResponse
        {
            Id = skill.Id,
            Key = skill.Key,
            Name = skill.Name,
            Description = skill.Description,
            Instructions = skill.Instructions,
            Files = files,
            IsSystem = skill.IsSystem,
            IsDisable = skill.IsDisable,
            CreateTime = skill.CreateTime,
            UpdateTime = skill.UpdateTime,
        };
    }
}

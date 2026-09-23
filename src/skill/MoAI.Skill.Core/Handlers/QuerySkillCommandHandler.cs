using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Account.Services;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Skill.Queries;
using MoAI.Skill.Queries.Responses;
using MoAI.Skill.Services;
using MoAI.Team.Services;

namespace MoAI.Skill.Handlers;

/// <summary>
/// <inheritdoc cref="QuerySkillCommand"/>
/// </summary>
public class QuerySkillCommandHandler : IRequestHandler<QuerySkillCommand, QuerySkillCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IUserAccountService _userAccountService;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuerySkillCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="userAccountService">用户账号领域服务.</param>
    /// <param name="teamService">团队领域服务.</param>
    public QuerySkillCommandHandler(DatabaseContext databaseContext, IUserAccountService userAccountService, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _userAccountService = userAccountService;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<QuerySkillCommandResponse> Handle(QuerySkillCommand request, CancellationToken cancellationToken)
    {
        var skill = await _databaseContext.Skills.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.SkillId, cancellationToken)
            ?? throw new BusinessException("技能不存在.") { StatusCode = 404 };

        await SkillAccessGuard.EnsureCanViewAsync(skill, request.ContextUserId, _userAccountService, _teamService, cancellationToken);

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
            TeamId = skill.TeamId,
            ClassifyId = skill.ClassifyId,
            AvatarPath = skill.AvatarPath ?? string.Empty,
            IsPublic = skill.IsPublic,
            CreateTime = skill.CreateTime,
            UpdateTime = skill.UpdateTime,
        };
    }
}

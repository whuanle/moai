using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Account.Services;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Skill.Commands;
using MoAI.Skill.Services;
using MoAI.Team.Services;

namespace MoAI.Skill.Handlers;

/// <summary>
/// <inheritdoc cref="DeleteSkillCommand"/>
/// </summary>
public class DeleteSkillCommandHandler : IRequestHandler<DeleteSkillCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IUserAccountService _userAccountService;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteSkillCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="userAccountService">用户账号领域服务.</param>
    /// <param name="teamService">团队领域服务.</param>
    public DeleteSkillCommandHandler(DatabaseContext databaseContext, IUserAccountService userAccountService, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _userAccountService = userAccountService;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeleteSkillCommand request, CancellationToken cancellationToken)
    {
        var skill = await _databaseContext.Skills.FirstOrDefaultAsync(x => x.Id == request.SkillId, cancellationToken)
            ?? throw new BusinessException("技能不存在.") { StatusCode = 404 };

        if (skill.IsSystem)
        {
            throw new BusinessException("系统内置技能不可删除，可使用禁用.") { StatusCode = 400 };
        }

        await SkillAccessGuard.EnsureCanManageAsync(skill, request.ContextUserId, _userAccountService, _teamService, cancellationToken);

        _databaseContext.Skills.Remove(skill);

        // 同步移除该技能待审核的上架申请，避免审核列表出现僵尸记录
        var skillIdString = skill.Id.ToString();
        var pendingReviews = await _databaseContext.PublicationReviews
            .Where(x => x.ResourceType == (int)PublicationResourceType.Skill
                && x.ResourceId == skillIdString
                && x.State == (int)PublicationState.Pending)
            .ToListAsync(cancellationToken);

        _databaseContext.PublicationReviews.RemoveRange(pendingReviews);

        await _databaseContext.SaveChangesAsync(cancellationToken);
        return EmptyCommandResponse.Default;
    }
}

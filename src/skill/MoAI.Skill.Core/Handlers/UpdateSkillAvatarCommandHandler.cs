using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Account.Services;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Skill.Commands;
using MoAI.Skill.Services;
using MoAI.Team.Services;

namespace MoAI.Skill.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateSkillAvatarCommand"/>
/// </summary>
public class UpdateSkillAvatarCommandHandler : IRequestHandler<UpdateSkillAvatarCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IUserAccountService _userAccountService;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateSkillAvatarCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="userAccountService">用户账号领域服务.</param>
    /// <param name="teamService">团队领域服务.</param>
    public UpdateSkillAvatarCommandHandler(DatabaseContext databaseContext, IUserAccountService userAccountService, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _userAccountService = userAccountService;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateSkillAvatarCommand request, CancellationToken cancellationToken)
    {
        var skill = await _databaseContext.Skills
            .FirstOrDefaultAsync(x => x.Id == request.SkillId, cancellationToken);

        if (skill == null)
        {
            throw new BusinessException("技能不存在.") { StatusCode = 404 };
        }

        await SkillAccessGuard.EnsureCanManageAsync(skill, request.ContextUserId, _userAccountService, _teamService, cancellationToken);

        // 仅允许引用已完成上传并登记的文件，防止任意伪造 objectKey（与应用/知识库头像同规则）
        var fileExists = await _databaseContext.Files
            .AnyAsync(f => f.ObjectKey == request.ObjectKey && f.IsUploaded, cancellationToken);

        if (!fileExists)
        {
            throw new BusinessException("头像文件不存在或未完成上传.") { StatusCode = 404 };
        }

        skill.AvatarPath = request.ObjectKey;
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}

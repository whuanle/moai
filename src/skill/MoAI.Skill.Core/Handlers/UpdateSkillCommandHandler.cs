using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Account.Services;
using MoAI.Classify;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Skill.Commands;
using MoAI.Skill.Services;
using MoAI.Team.Services;

namespace MoAI.Skill.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateSkillCommand"/>
/// </summary>
public class UpdateSkillCommandHandler : IRequestHandler<UpdateSkillCommand, EmptyCommandResponse>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly DatabaseContext _databaseContext;
    private readonly IUserAccountService _userAccountService;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateSkillCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="userAccountService">用户账号领域服务.</param>
    /// <param name="teamService">团队领域服务.</param>
    public UpdateSkillCommandHandler(DatabaseContext databaseContext, IUserAccountService userAccountService, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _userAccountService = userAccountService;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateSkillCommand request, CancellationToken cancellationToken)
    {
        var skill = await _databaseContext.Skills.FirstOrDefaultAsync(x => x.Id == request.SkillId, cancellationToken)
            ?? throw new BusinessException("技能不存在.") { StatusCode = 404 };

        if (skill.IsSystem)
        {
            throw new BusinessException("系统内置技能不可修改.") { StatusCode = 400 };
        }

        await SkillAccessGuard.EnsureCanManageAsync(skill, request.ContextUserId, _userAccountService, _teamService, cancellationToken);
        await SkillFilesGuard.EnsureFilesValidAsync(_databaseContext, request.Files, cancellationToken);

        if (request.ClassifyId > 0)
        {
            var classifyExist = await _databaseContext.Classifies
                .AnyAsync(x => x.Id == request.ClassifyId && x.Type == ClassifyTypes.Skill, cancellationToken);

            if (!classifyExist)
            {
                throw new BusinessException("技能分类不存在.") { StatusCode = 404 };
            }
        }

        skill.Name = request.Name;
        skill.Description = request.Description ?? string.Empty;
        skill.Instructions = request.Instructions ?? string.Empty;
        skill.Files = JsonSerializer.Serialize(request.Files, JsonOptions);
        skill.ClassifyId = request.ClassifyId;

        await _databaseContext.SaveChangesAsync(cancellationToken);
        return EmptyCommandResponse.Default;
    }
}

using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Account.Services;
using MoAI.Classify;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Skill.Commands;
using MoAI.Skill.Models;
using MoAI.Skill.Services;
using MoAI.Team.Services;

namespace MoAI.Skill.Handlers;

/// <summary>
/// <inheritdoc cref="CreateSkillCommand"/>
/// </summary>
public class CreateSkillCommandHandler : IRequestHandler<CreateSkillCommand, SimpleGuid>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly DatabaseContext _databaseContext;
    private readonly IUserAccountService _userAccountService;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateSkillCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="userAccountService">用户账号领域服务.</param>
    /// <param name="teamService">团队领域服务.</param>
    public CreateSkillCommandHandler(DatabaseContext databaseContext, IUserAccountService userAccountService, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _userAccountService = userAccountService;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<SimpleGuid> Handle(CreateSkillCommand request, CancellationToken cancellationToken)
    {
        await SkillAccessGuard.EnsureCanCreateAsync(request.TeamId, request.ContextUserId, _userAccountService, _teamService, cancellationToken);

        var keyExist = await _databaseContext.Skills.AnyAsync(x => x.Key == request.Key, cancellationToken);
        if (keyExist)
        {
            throw new BusinessException("技能标识已存在，请更换后重试.") { StatusCode = 409 };
        }

        if (request.ClassifyId > 0)
        {
            var classifyExist = await _databaseContext.Classifies
                .AnyAsync(x => x.Id == request.ClassifyId && x.Type == ClassifyTypes.Skill, cancellationToken);

            if (!classifyExist)
            {
                throw new BusinessException("技能分类不存在.") { StatusCode = 404 };
            }
        }

        await SkillFilesGuard.EnsureFilesValidAsync(_databaseContext, request.Files, cancellationToken);

        var skill = new SkillEntity
        {
            Id = Guid.CreateVersion7(),
            Key = request.Key,
            Name = request.Name,
            Description = request.Description ?? string.Empty,
            Instructions = request.Instructions ?? string.Empty,
            Files = JsonSerializer.Serialize(request.Files, JsonOptions),
            IsSystem = false,
            TeamId = request.TeamId,
            ClassifyId = request.ClassifyId,
            IsPublic = false,
            IsDisable = false,
        };

        _databaseContext.Skills.Add(skill);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return new SimpleGuid { Value = skill.Id };
    }
}

/// <summary>
/// 技能包文件校验：文件必须已上传完成（file 表存在且 IsUploaded）.
/// </summary>
internal static class SkillFilesGuard
{
    public static async Task EnsureFilesValidAsync(DatabaseContext databaseContext, IReadOnlyList<SkillFileItem> files, CancellationToken cancellationToken)
    {
        if (files.Count == 0)
        {
            return;
        }

        var fileIds = files.Select(f => f.FileId).Distinct().ToArray();
        var uploadedCount = await databaseContext.Files
            .CountAsync(x => fileIds.Contains(x.Id) && x.IsUploaded, cancellationToken);

        if (uploadedCount != fileIds.Length)
        {
            throw new BusinessException("存在未上传完成的技能包文件.") { StatusCode = 400 };
        }
    }
}

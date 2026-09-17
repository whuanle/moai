using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Account.Services;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Skill.Queries;
using MoAI.Skill.Queries.Responses;
using MoAI.Skill.Services;
using MoAI.Storage.Services;
using MoAI.Team.Services;

namespace MoAI.Skill.Handlers;

/// <summary>
/// <inheritdoc cref="QuerySkillFileDownloadCommand"/>
/// </summary>
public class QuerySkillFileDownloadCommandHandler : IRequestHandler<QuerySkillFileDownloadCommand, QuerySkillFileDownloadResponse>
{
    /// <summary>
    /// 下载地址有效期.
    /// </summary>
    private static readonly TimeSpan Expiry = TimeSpan.FromHours(1);

    private readonly DatabaseContext _databaseContext;
    private readonly IStorageService _storageService;
    private readonly IUserAccountService _userAccountService;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuerySkillFileDownloadCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="storageService">存储领域服务.</param>
    /// <param name="userAccountService">用户账号领域服务.</param>
    /// <param name="teamService">团队领域服务.</param>
    public QuerySkillFileDownloadCommandHandler(
        DatabaseContext databaseContext,
        IStorageService storageService,
        IUserAccountService userAccountService,
        ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _storageService = storageService;
        _userAccountService = userAccountService;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<QuerySkillFileDownloadResponse> Handle(QuerySkillFileDownloadCommand request, CancellationToken cancellationToken)
    {
        var skill = await _databaseContext.Skills.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.SkillId, cancellationToken)
            ?? throw new BusinessException("技能不存在.") { StatusCode = 404 };

        if (skill.IsSystem)
        {
            throw new BusinessException("系统内置技能不支持下载.") { StatusCode = 400 };
        }

        await SkillAccessGuard.EnsureCanViewAsync(skill, request.ContextUserId, _userAccountService, _teamService, cancellationToken);

        var fileItems = SkillService.ParseFiles(skill.Files);
        if (fileItems.Count == 0)
        {
            return new QuerySkillFileDownloadResponse { Key = skill.Key, Items = Array.Empty<SkillFileDownloadItem>() };
        }

        var fileIds = fileItems.Select(x => x.FileId).Distinct().ToArray();
        var objectKeys = await _databaseContext.Files
            .AsNoTracking()
            .Where(x => fileIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.ObjectKey, cancellationToken);

        var items = new List<SkillFileDownloadItem>(fileItems.Count);
        foreach (var file in fileItems)
        {
            if (!objectKeys.TryGetValue(file.FileId, out var objectKey))
            {
                throw new BusinessException($"技能文件不存在: {file.Path}") { StatusCode = 404 };
            }

            var downloadUrl = await _storageService.GetDownloadUrlAsync(objectKey, file.FileName, Expiry, cancellationToken);
            items.Add(new SkillFileDownloadItem
            {
                Path = file.Path,
                FileName = file.FileName,
                DownloadUrl = downloadUrl,
            });
        }

        return new QuerySkillFileDownloadResponse { Key = skill.Key, Items = items };
    }
}

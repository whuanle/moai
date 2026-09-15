using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Account.Services;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Prompt.Queries.Responses;
using MoAI.Team.Services;

namespace MoAI.Prompt.Queries;

/// <summary>
/// <inheritdoc cref="QueryPromptCommand"/>
/// </summary>
public class QueryPromptCommandHandler : IRequestHandler<QueryPromptCommand, QueryPromptCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IUserInfoFillService _userInfoFillService;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryPromptCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="userInfoFillService">用户信息填充领域服务.</param>
    /// <param name="teamService">团队领域服务.</param>
    public QueryPromptCommandHandler(DatabaseContext databaseContext, IUserInfoFillService userInfoFillService, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _userInfoFillService = userInfoFillService;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<QueryPromptCommandResponse> Handle(QueryPromptCommand request, CancellationToken cancellationToken)
    {
        var prompt = await _databaseContext.Prompts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.PromptId, cancellationToken);

        if (prompt == null)
        {
            throw new BusinessException("提示词不存在.") { StatusCode = 404 };
        }

        var isOwner = prompt.TeamId == 0 && prompt.CreateUserId == request.ContextUserId;
        var isTeamMember = false;
        if (prompt.TeamId > 0)
        {
            isTeamMember = await _teamService.GetMyRoleAsync(prompt.TeamId, request.ContextUserId, cancellationToken) != null;
        }

        if (!isOwner && !isTeamMember && !prompt.IsPublic)
        {
            // 无权限时不区分不存在/不可见，统一 404 避免探测
            throw new BusinessException("提示词不存在.") { StatusCode = 404 };
        }

        // 仅通过市场（公开）可见时计一次使用数
        if (!isOwner && !isTeamMember)
        {
            var tracked = await _databaseContext.Prompts
                .FirstOrDefaultAsync(x => x.Id == request.PromptId, cancellationToken);
            if (tracked != null)
            {
                tracked.Counter++;
                await _databaseContext.SaveChangesAsync(cancellationToken);
                prompt = tracked;
            }
        }

        var item = new QueryPromptCommandResponse
        {
            PromptId = prompt.Id,
            Name = prompt.Name,
            Description = prompt.Description,
            AvatarPath = prompt.AvatarPath,
            PromptClassId = prompt.PromptClassId,
            IsPublic = prompt.IsPublic,
            Counter = prompt.Counter,
            TeamId = prompt.TeamId,
            Content = prompt.Content,
            CreateUserId = (int)prompt.CreateUserId,
            CreateTime = prompt.CreateTime,
            UpdateUserId = (int)prompt.UpdateUserId,
            UpdateTime = prompt.UpdateTime,
        };

        await _userInfoFillService.FillAsync([item], cancellationToken);
        return item;
    }
}

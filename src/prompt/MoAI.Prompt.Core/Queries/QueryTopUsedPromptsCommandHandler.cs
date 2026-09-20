using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Account.Services;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Prompt.Queries.Responses;
using MoAI.Team.Services;

namespace MoAI.Prompt.Queries;

/// <summary>
/// <inheritdoc cref="QueryTopUsedPromptsCommand"/>
/// </summary>
public class QueryTopUsedPromptsCommandHandler : IRequestHandler<QueryTopUsedPromptsCommand, QueryPromptListCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IUserInfoFillService _userInfoFillService;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryTopUsedPromptsCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="userInfoFillService">用户信息填充领域服务.</param>
    /// <param name="teamService">团队领域服务.</param>
    public QueryTopUsedPromptsCommandHandler(DatabaseContext databaseContext, IUserInfoFillService userInfoFillService, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _userInfoFillService = userInfoFillService;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<QueryPromptListCommandResponse> Handle(QueryTopUsedPromptsCommand request, CancellationToken cancellationToken)
    {
        // 与 team_list 一致：仅团队成员可查（非成员 404，避免跨团队枚举提示词）
        var myRole = await _teamService.GetMyRoleAsync(request.TeamId, request.ContextUserId, cancellationToken);
        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        // 范围与「可用专家」一致：本人个人提示词 + 当前团队提示词；
        // 按使用次数降序取前 N（零使用的也参与排序补位，保证新对话的推荐列表不为空）
        var entities = await _databaseContext.Prompts
            .AsNoTracking()
            .Where(x => x.TeamId == request.TeamId || (x.TeamId == 0 && x.CreateUserId == request.ContextUserId))
            .OrderByDescending(x => x.UseCount)
            .ThenByDescending(x => x.Id)
            .Take(request.Limit)
            .ToListAsync(cancellationToken);

        var items = await QueryPromptListHelper.MapAsync(entities, _databaseContext, _userInfoFillService, cancellationToken);

        return new QueryPromptListCommandResponse { Items = items };
    }
}

using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Account.Services;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Publication.Queries.Responses;
using MoAI.Team.Services;

namespace MoAI.Publication.Queries;

/// <summary>
/// <inheritdoc cref="QueryPublicationTeamListCommand"/>
/// </summary>
public class QueryPublicationTeamListCommandHandler : IRequestHandler<QueryPublicationTeamListCommand, QueryPublicationReviewListResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IUserInfoFillService _userInfoFillService;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryPublicationTeamListCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="userInfoFillService">用户信息填充领域服务.</param>
    /// <param name="teamService">团队领域服务.</param>
    public QueryPublicationTeamListCommandHandler(DatabaseContext databaseContext, IUserInfoFillService userInfoFillService, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _userInfoFillService = userInfoFillService;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<QueryPublicationReviewListResponse> Handle(QueryPublicationTeamListCommand request, CancellationToken cancellationToken)
    {
        var myRole = await _teamService.GetMyRoleAsync(request.TeamId, request.ContextUserId, cancellationToken);

        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        var query = _databaseContext.PublicationReviews
            .AsNoTracking()
            .Where(x => x.TeamId == request.TeamId);

        if (request.State != null)
        {
            query = query.Where(x => x.State == (int)request.State);
        }

        if (request.ResourceType != null)
        {
            query = query.Where(x => x.ResourceType == (int)request.ResourceType);
        }

        var entities = await query
            .OrderByDescending(x => x.CreateTime)
            .ToListAsync(cancellationToken);

        var items = await PublicationReviewListHelper.MapAsync(entities, _databaseContext, _userInfoFillService, cancellationToken);

        return new QueryPublicationReviewListResponse { Items = items };
    }
}

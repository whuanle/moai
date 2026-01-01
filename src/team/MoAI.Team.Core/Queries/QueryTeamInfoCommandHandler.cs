using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Storage.Queries;
using MoAI.Store.Queries;
using MoAI.Team.Models;
using MoAI.Team.Queries.Responses;

namespace MoAI.Team.Queries;

/// <summary>
/// <inheritdoc cref="QueryTeamInfoCommand"/>
/// </summary>
public class QueryTeamInfoCommandHandler : IRequestHandler<QueryTeamInfoCommand, QueryTeamInfoCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryTeamInfoCommandHandler"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="databaseContext"></param>
    public QueryTeamInfoCommandHandler(DatabaseContext databaseContext, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<QueryTeamInfoCommandResponse> Handle(QueryTeamInfoCommand request, CancellationToken cancellationToken)
    {
        var team = await _databaseContext.Teams
            .FirstOrDefaultAsync(x => x.Id == request.TeamId, cancellationToken);

        if (team == null)
        {
            throw new BusinessException("团队不存在") { StatusCode = 404 };
        }

        // 获取用户在团队的角色
        var userTeamRole = await _databaseContext.TeamUsers
            .Where(x => x.TeamId == request.TeamId && x.UserId == request.ContextUserId)
            .Select(x => (TeamRole?)x.Role)
            .FirstOrDefaultAsync(cancellationToken);

        // 获取成员数量
        var memberCount = await _databaseContext.TeamUsers
            .CountAsync(x => x.TeamId == request.TeamId, cancellationToken);

        var response = new QueryTeamInfoCommandResponse
        {
            Id = team.Id,
            Name = team.Name,
            Description = team.Description,
            AvatarKey = team.Avatar,
            CreateUserId = team.CreateUserId,
            CreateTime = team.CreateTime,
            MyRole = userTeamRole ?? TeamRole.None,
            MemberCount = memberCount
        };

        await _mediator.Send(new QueryAvatarUrlCommand { Items = new[] { response } });
        return response;
    }
}

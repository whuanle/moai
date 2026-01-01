using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Storage.Queries;
using MoAI.Team.Models;
using MoAI.Team.Queries.Responses;

namespace MoAI.Team.Queries;

/// <summary>
/// <inheritdoc cref="QueryTeamMemberListCommand"/>
/// </summary>
public class QueryTeamMemberListCommandHandler : IRequestHandler<QueryTeamMemberListCommand, QueryTeamMemberListCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryTeamMemberListCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    public QueryTeamMemberListCommandHandler(DatabaseContext databaseContext, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<QueryTeamMemberListCommandResponse> Handle(QueryTeamMemberListCommand request, CancellationToken cancellationToken)
    {
        // 查询团队成员
        var members = await (from tu in _databaseContext.TeamUsers
                             join u in _databaseContext.Users on tu.UserId equals u.Id
                             where tu.TeamId == request.TeamId
                             orderby tu.Role descending, tu.CreateTime
                             select new QueryTeamMemberListQueryResponseItem
                             {
                                 UserId = tu.UserId,
                                 UserName = u.UserName,
                                 AvatarKey = u.AvatarPath,
                                 Role = (TeamRole)tu.Role,
                                 JoinTime = tu.CreateTime
                             }).ToListAsync(cancellationToken);

        await _mediator.Send(new QueryAvatarUrlCommand { Items = members });

        return new QueryTeamMemberListCommandResponse { Items = members };
    }
}

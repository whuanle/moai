using Azure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Helpers;
using MoAI.Login.Commands;
using MoAI.Storage.Queries;
using MoAI.Team.Models;
using MoAI.Team.Queries;
using MoAI.User.Queries;
using MoAI.Wiki.Wikis.Queries.Response;

namespace MoAI.Wiki.Wikis.Queries;

/// <summary>
/// <inheritdoc cref="QueryWikiBaseListCommand"/>.
/// </summary>
public class QueryWikiBaseListCommandHandler : IRequestHandler<QueryWikiBaseListCommand, IReadOnlyCollection<QueryWikiInfoResponse>>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWikiBaseListCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    public QueryWikiBaseListCommandHandler(DatabaseContext databaseContext, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<QueryWikiInfoResponse>> Handle(QueryWikiBaseListCommand request, CancellationToken cancellationToken)
    {
        /*
         1，私有知识库
         2，团队知识库
         3，无关，但是公开的知识库
         */

        var query = _databaseContext.Wikis.AsQueryable();

        if (request.IsOwn == true)
        {
            query = query.Where(x => x.TeamId == 0 && x.CreateUserId == request.ContextUserId);
        }
        else if (request.TeamId != 0 && request.TeamId > 0)
        {
            // 判断是否团队成员
            var existInTeam = await _mediator.Send(new QueryUserTeamRoleCommand { ContextUserId = request.ContextUserId, TeamId = request.TeamId.Value }, cancellationToken);
            if (existInTeam.Role == Team.Models.TeamRole.None)
            {
                throw new BusinessException("非知识库成员");
            }

            query = query.Where(x => x.TeamId == request.TeamId);
        }
        else
        {
            query = query.Where(x => x.IsPublic || (x.TeamId == 0 && x.CreateUserId == request.ContextUserId) || _databaseContext.TeamUsers.Where(a => a.UserId == request.ContextUserId && x.TeamId == a.TeamId).Any());
        }

        // 列表查询不返回所有字段.
        var wikis = await query
        .OrderBy(x => x.Name)
        .Select(x => new QueryWikiInfoResponse
        {
            WikiId = x.Id,
            Name = x.Name,
            Description = x.Description,
            IsPublic = x.IsPublic,
            CreateUserId = x.CreateUserId,
            UpdateUserId = x.UpdateUserId,
            CreateTime = x.CreateTime,
            UpdateTime = x.UpdateTime,
            TeamId = x.TeamId,
            AvatarKey = x.Avatar,
            DocumentCount = _databaseContext.WikiDocuments.Where(a => a.WikiId == x.Id).Count()
        })
        .ToListAsync(cancellationToken);

        foreach (var item in wikis)
        {
            if (item.TeamId == 0)
            {
                item.Role = item.CreateUserId == request.ContextUserId ? TeamRole.Owner : TeamRole.None;
            }
        }

        await _mediator.Send(new QueryAvatarUrlCommand { Items = wikis });
        await _mediator.Send(new FillUserInfoCommand { Items = wikis });

        return wikis;
    }
}

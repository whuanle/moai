using AngleSharp.Io;
using Azure;
using LinqKit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Helpers;
using MoAI.Login.Commands;
using MoAI.Storage.Queries;
using MoAI.Team.Models;
using MoAI.Team.Queries;
using MoAI.User.Queries;
using MoAI.Wiki.Wikis.Queries.Response;
using System;

namespace MoAI.Wiki.Wikis.Queries;

/// <summary>
/// <inheritdoc cref="QueryTeamWikiBaseListCommand"/>.
/// </summary>
public class QueryTeamWikiBaseListCommandHandler : IRequestHandler<QueryTeamWikiBaseListCommand, IReadOnlyCollection<QueryWikiInfoResponse>>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryTeamWikiBaseListCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    public QueryTeamWikiBaseListCommandHandler(DatabaseContext databaseContext, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<QueryWikiInfoResponse>> Handle(QueryTeamWikiBaseListCommand request, CancellationToken cancellationToken)
    {
        var responses = await _databaseContext.Wikis.Where(x => x.TeamId == request.TeamId)
            .DynamicOrder(request.OrderByFields)
                .LeftJoin(_databaseContext.Teams, a => a.TeamId, b => b.Id, (x, y) => new QueryWikiInfoResponse
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
                    Counter = x.Counter,
                    TeamName = y.Name,
                    Role = TeamRole.Collaborator,
                    DocumentCount = _databaseContext.WikiDocuments.Where(a => a.WikiId == x.Id).Count()
                })
                .ToListAsync(cancellationToken);

        await _mediator.Send(new QueryAvatarUrlCommand
        {
            Items = responses
        });
        await _mediator.Send(new FillUserInfoCommand { Items = responses });

        return responses;
    }
}

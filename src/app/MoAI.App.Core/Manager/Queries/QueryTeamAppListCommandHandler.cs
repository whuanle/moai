using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Manager.Models;
using MoAI.App.Models;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Storage.Queries;

namespace MoAI.App.Manager.Queries;

/// <summary>
/// <inheritdoc cref="QueryTeamAppListCommand"/>
/// </summary>
public class QueryTeamAppListCommandHandler : IRequestHandler<QueryTeamAppListCommand, QueryTeamAppListCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryTeamAppListCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    public QueryTeamAppListCommandHandler(DatabaseContext databaseContext, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<QueryTeamAppListCommandResponse> Handle(QueryTeamAppListCommand request, CancellationToken cancellationToken)
    {
        var query = _databaseContext.Apps
            .Where(x => x.TeamId == request.TeamId);

        if (request.IsForeign.HasValue)
        {
            query = query.Where(x => x.IsForeign == request.IsForeign.Value);
        }

        var apps = await query
            .Select((app) => new QueryAppListCommandResponseItem
            {
                AppId = app.Id,
                Name = app.Name,
                Description = app.Description,
                AvatarKey = app.Avatar,
                IsForeign = app.IsForeign,
                AppType = (AppType)app.AppType,
                IsDisable = app.IsDisable,
                IsPublic = app.IsPublic,
                CreateTime = app.CreateTime,
                CreateUserId = app.CreateUserId,
                UpdateUserId = app.UpdateUserId,
                UpdateTime = app.UpdateTime
            })
            .OrderByDescending(x => x.UpdateTime)
            .ToListAsync(cancellationToken);

        await _mediator.Send(new QueryAvatarUrlCommand { Items = apps });

        return new QueryTeamAppListCommandResponse
        {
            Items = apps
        };
    }
}

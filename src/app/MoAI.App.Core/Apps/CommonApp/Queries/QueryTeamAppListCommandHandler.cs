using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Apps.CommonApp.Responses;
using MoAI.Database;
using MoAI.Storage.Queries;

namespace MoAI.App.Apps.CommonApp.Queries;

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
        var apps = await _databaseContext.Apps
            .Where(x => x.TeamId == request.TeamId && !x.IsDisable && !x.IsForeign)
            .OrderByDescending(x => x.UpdateTime)
            .Select(x => new TeamAppItem
            {
                Id = x.Id,
                Name = x.Name,
                Description = x.Description,
                AvatarKey = x.Avatar,
                AppType = x.AppType,
                CreateTime = x.CreateTime,
                UpdateTime = x.UpdateTime,
            })
            .ToListAsync(cancellationToken);

        await _mediator.Send(new QueryAvatarUrlCommand { Items = apps }, cancellationToken);

        return new QueryTeamAppListCommandResponse { Items = apps };
    }
}

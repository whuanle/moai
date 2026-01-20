using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Apps.CommonApp.Queries;
using MoAI.App.Apps.CommonApp.Responses;
using MoAI.Database;
using MoAI.Storage.Queries;
using MoAI.User.Queries;

namespace MoAI.App.AppCommon.Queries;

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
        var query = _databaseContext.Apps.Where(x => x.TeamId == request.TeamId);
        if (request.ClassifyId != null)
        {
            query = query.Where(x => x.ClassifyId == request.ClassifyId);
        }

        if (request.AppType != null)
        {
            query = query.Where(x => x.AppType == (int)request.AppType.Value);
        }

        if (request.IsForeign != null)
        {
            query = query.Where(x => x.IsForeign == request.IsForeign.Value);
        }

        var apps = await query
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
                CreateUserId = x.CreateUserId,
                UpdateUserId = x.UpdateUserId,
                IsDisable = x.IsDisable,
                IsForeign = x.IsForeign
            })
            .ToListAsync(cancellationToken);

        await _mediator.Send(new QueryAvatarUrlCommand { Items = apps }, cancellationToken);
        await _mediator.Send(new FillUserInfoCommand { Items = apps }, cancellationToken);

        return new QueryTeamAppListCommandResponse { Items = apps };
    }
}

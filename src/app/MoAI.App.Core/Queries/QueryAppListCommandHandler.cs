using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Models;
using MoAI.App.Queries;
using MoAI.App.Queries.Responses;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Storage.Queries;

namespace MoAI.App.Queries;

/// <summary>
/// <inheritdoc cref="QueryAppListCommand"/>
/// </summary>
public class QueryAppListCommandHandler : IRequestHandler<QueryAppListCommand, QueryAppListCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAppListCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    public QueryAppListCommandHandler(DatabaseContext databaseContext, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<QueryAppListCommandResponse> Handle(QueryAppListCommand request, CancellationToken cancellationToken)
    {
        var query = _databaseContext.Apps
            .Where(x => x.TeamId == request.TeamId);

        if (request.IsForeign.HasValue)
        {
            query = query.Where(x => x.IsForeign == request.IsForeign.Value);
        }

        var apps = await query
            .Join(
                _databaseContext.AppCommons,
                app => app.Id,
                common => common.AppId,
                (app, common) => new QueryAppListCommandResponseItem
                {
                    AppId = app.Id,
                    Name = app.Name,
                    Description = app.Description,
                    AvatarKey = app.Avatar,
                    IsForeign = app.IsForeign,
                    AppType = (AppType)app.AppType,
                    IsDisable = app.IsDisable,
                    IsPublic = app.IsPublic,
                    CreateTime = app.CreateTime
                })
            .OrderByDescending(x => x.CreateTime)
            .ToListAsync(cancellationToken);

        await _mediator.Send(new QueryAvatarUrlCommand { Items = apps });

        return new QueryAppListCommandResponse
        {
            Items = apps
        };
    }
}

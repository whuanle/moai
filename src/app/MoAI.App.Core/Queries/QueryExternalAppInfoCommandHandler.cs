using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Queries.Responses;
using MoAI.Database;
using MoAI.Storage.Queries;

namespace MoAI.App.Queries;

/// <summary>
/// <inheritdoc cref="QueryExternalAppInfoCommand"/>
/// </summary>
public class QueryExternalAppInfoCommandHandler : IRequestHandler<QueryExternalAppInfoCommand, QueryExternalAppInfoCommandResponse?>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryExternalAppInfoCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    public QueryExternalAppInfoCommandHandler(DatabaseContext databaseContext, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<QueryExternalAppInfoCommandResponse?> Handle(QueryExternalAppInfoCommand request, CancellationToken cancellationToken)
    {
        var result = await _databaseContext.ExternalApps
            .Where(x => x.TeamId == request.TeamId)
            .Select(x => new QueryExternalAppInfoCommandResponse
            {
                AppId = x.Id,
                Name = x.Name,
                Description = x.Description,
                AvatarKey = x.Avatar,
                Key = x.Key,
                IsDisable = x.IsDsiable,
                CreateTime = x.CreateTime,
                UpdateTime = x.UpdateTime
            })
            .FirstOrDefaultAsync(cancellationToken);

        await _mediator.Send(request: new QueryAvatarUrlCommand { Items = new[] { result } });

        return result;
    }
}

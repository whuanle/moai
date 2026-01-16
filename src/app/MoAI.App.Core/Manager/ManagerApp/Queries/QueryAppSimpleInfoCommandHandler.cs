using Azure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Manager.ManagerApp.Models;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Storage.Queries;

namespace MoAI.App.Manager.ManagerApp.Queries;

/// <summary>
/// <inheritdoc cref="QueryAppSimpleInfoCommand"/>
/// </summary>
public class QueryAppSimpleInfoCommandHandler : IRequestHandler<QueryAppSimpleInfoCommand, QueryAppSimpleInfoCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAppSimpleInfoCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    public QueryAppSimpleInfoCommandHandler(DatabaseContext databaseContext, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<QueryAppSimpleInfoCommandResponse> Handle(QueryAppSimpleInfoCommand request, CancellationToken cancellationToken)
    {
        var result = await _databaseContext.Apps
            .Where(x => x.Id == request.AppId && x.TeamId == request.TeamId)
            .Join(
                _databaseContext.AppCommons,
                app => app.Id,
                common => common.AppId,
                (app, common) => new QueryAppSimpleInfoCommandResponse
                {
                    AppId = app.Id,
                    Name = app.Name,
                    Description = app.Description,
                    Avatar = app.Avatar
                })
            .FirstOrDefaultAsync(cancellationToken);

        if (result == null)
        {
            throw new BusinessException("应用不存在") { StatusCode = 404 };
        }

        await _mediator.Send(request: new QueryAvatarUrlCommand { Items = new[] { result } });

        return result;
    }
}

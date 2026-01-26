using Azure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Manager.Models;
using MoAI.App.Models;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Storage.Queries;

namespace MoAI.App.AppStore.Queries;

/// <summary>
/// <inheritdoc cref="QueryOneAppSimpleInfoCommand"/>
/// </summary>
public class QueryOneAppSimpleInfoCommandHandler : IRequestHandler<QueryOneAppSimpleInfoCommand, QueryAppListCommandResponseItem>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryOneAppSimpleInfoCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    public QueryOneAppSimpleInfoCommandHandler(DatabaseContext databaseContext, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<QueryAppListCommandResponseItem> Handle(QueryOneAppSimpleInfoCommand request, CancellationToken cancellationToken)
    {
        var result = await _databaseContext.Apps
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
            .FirstOrDefaultAsync(cancellationToken);

        if (result == null)
        {
            throw new BusinessException("应用不存在") { StatusCode = 404 };
        }

        await _mediator.Send(request: new QueryAvatarUrlCommand { Items = new[] { result } });

        return result;
    }
}

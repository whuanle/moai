using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Queries;
using MoAI.App.Queries.Responses;
using MoAI.Database;
using MoAI.Infra.Exceptions;

namespace MoAI.App.Handlers;

/// <summary>
/// <inheritdoc cref="QueryExternalAgentSessionsCommand"/>
/// </summary>
public class QueryExternalAgentSessionsCommandHandler : IRequestHandler<QueryExternalAgentSessionsCommand, QueryExternalAgentSessionsCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryExternalAgentSessionsCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public QueryExternalAgentSessionsCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QueryExternalAgentSessionsCommandResponse> Handle(QueryExternalAgentSessionsCommand request, CancellationToken cancellationToken)
    {
        var externalUserId = ExternalAppAccessValidator.EnsureExternalUser(request.Context);

        if (!request.Context.IsAppAuthorized(request.AppId))
        {
            throw new BusinessException("该应用不在授权范围内.") { StatusCode = 403 };
        }

        var appExists = await _databaseContext.Apps
            .AnyAsync(x => x.Id == request.AppId, cancellationToken);
        if (!appExists)
        {
            throw new BusinessException("应用不存在.") { StatusCode = 404 };
        }

        var rows = await _databaseContext.AppAgentSessions
            .Where(x => x.AppId == request.AppId && x.CreateUserId == externalUserId)
            .OrderByDescending(x => x.LastMessageTime)
            .Select(x => new
            {
                x.Id,
                x.AppId,
                x.Title,
                x.UserType,
                x.InputTokens,
                x.OutTokens,
                x.TotalTokens,
                x.LastMessageTime,
                x.CreateTime
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(x => new AppSessionItem
            {
                SessionId = x.Id,
                AppId = x.AppId,
                Title = x.Title,
                UserType = x.UserType,
                InputTokens = x.InputTokens,
                OutTokens = x.OutTokens,
                TotalTokens = x.TotalTokens,
                LastMessageTime = x.LastMessageTime,
                CreateTime = x.CreateTime
            })
            .ToList();

        return new QueryExternalAgentSessionsCommandResponse
        {
            AppId = request.AppId,
            Items = items
        };
    }
}

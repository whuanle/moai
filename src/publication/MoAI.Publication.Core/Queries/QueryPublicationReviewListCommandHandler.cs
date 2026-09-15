using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Account.Services;
using MoAI.Database;
using MoAI.Publication.Queries.Responses;

namespace MoAI.Publication.Queries;

/// <summary>
/// <inheritdoc cref="QueryPublicationReviewListCommand"/>
/// </summary>
public class QueryPublicationReviewListCommandHandler : IRequestHandler<QueryPublicationReviewListCommand, QueryPublicationReviewListResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IUserInfoFillService _userInfoFillService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryPublicationReviewListCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="userInfoFillService">用户信息填充领域服务.</param>
    public QueryPublicationReviewListCommandHandler(DatabaseContext databaseContext, IUserInfoFillService userInfoFillService)
    {
        _databaseContext = databaseContext;
        _userInfoFillService = userInfoFillService;
    }

    /// <inheritdoc/>
    public async Task<QueryPublicationReviewListResponse> Handle(QueryPublicationReviewListCommand request, CancellationToken cancellationToken)
    {
        var query = _databaseContext.PublicationReviews.AsNoTracking().AsQueryable();

        if (request.State != null)
        {
            query = query.Where(x => x.State == (int)request.State);
        }

        if (request.ResourceType != null)
        {
            query = query.Where(x => x.ResourceType == (int)request.ResourceType);
        }

        var entities = await query
            .OrderByDescending(x => x.CreateTime)
            .ToListAsync(cancellationToken);

        var items = await PublicationReviewListHelper.MapAsync(entities, _databaseContext, _userInfoFillService, cancellationToken);

        return new QueryPublicationReviewListResponse { Items = items };
    }
}

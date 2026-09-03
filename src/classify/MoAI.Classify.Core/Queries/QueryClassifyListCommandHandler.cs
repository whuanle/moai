using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Account.Services;
using MoAI.Classify.Queries;
using MoAI.Classify.Queries.Responses;
using MoAI.Database;

namespace MoAI.Classify.Queries;

/// <summary>
/// <inheritdoc cref="QueryClassifyListCommand"/>
/// </summary>
public class QueryClassifyListCommandHandler : IRequestHandler<QueryClassifyListCommand, QueryClassifyListCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IUserInfoFillService _userInfoFillService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryClassifyListCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="userInfoFillService">用户信息填充服务.</param>
    public QueryClassifyListCommandHandler(DatabaseContext databaseContext, IUserInfoFillService userInfoFillService)
    {
        _databaseContext = databaseContext;
        _userInfoFillService = userInfoFillService;
    }

    /// <inheritdoc/>
    public async Task<QueryClassifyListCommandResponse> Handle(QueryClassifyListCommand request, CancellationToken cancellationToken)
    {
        var query = _databaseContext.Classifies.AsQueryable();

        if (!string.IsNullOrEmpty(request.Type))
        {
            query = query.Where(x => x.Type == request.Type);
        }

        var items = await query
            .OrderBy(x => x.Id)
            .Select(x => new ClassifyItem
            {
                ClassifyId = x.Id,
                Type = x.Type,
                Name = x.Name,
                Description = x.Description,
                CreateUserId = (int)x.CreateUserId,
                UpdateUserId = (int)x.UpdateUserId,
                CreateTime = x.CreateTime,
                UpdateTime = x.UpdateTime,
            })
            .ToListAsync(cancellationToken);

        await _userInfoFillService.FillAsync(items, cancellationToken);

        return new QueryClassifyListCommandResponse { Items = items };
    }
}

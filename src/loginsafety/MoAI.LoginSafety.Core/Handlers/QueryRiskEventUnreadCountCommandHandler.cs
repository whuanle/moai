using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.LoginSafety.Commands;

namespace MoAI.LoginSafety.Handlers;

/// <summary>
/// 查询未读风险事件数量（菜单角标）.
/// </summary>
public class QueryRiskEventUnreadCountCommandHandler : IRequestHandler<QueryRiskEventUnreadCountCommand, QueryRiskEventUnreadCountCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryRiskEventUnreadCountCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public QueryRiskEventUnreadCountCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QueryRiskEventUnreadCountCommandResponse> Handle(QueryRiskEventUnreadCountCommand request, CancellationToken cancellationToken)
    {
        var count = await _databaseContext.LoginRiskEvents.AsNoTracking()
            .CountAsync(x => !x.IsRead, cancellationToken);

        return new QueryRiskEventUnreadCountCommandResponse { Count = count };
    }
}

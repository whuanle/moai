using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Models;
using System.Linq.Expressions;

namespace MoAI.Common.Queries;

public class QueryDataCreateUserIdCommandHandler<TEntity> : IRequestHandler<QueryDataCreateUserIdCommand<TEntity>, QueryDataCreateUserIdCommandResponse>
        where TEntity : AuditsInfo
{
    private readonly DatabaseContext _databaseContext;

    public QueryDataCreateUserIdCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    public async Task<QueryDataCreateUserIdCommandResponse> Handle(Queries.QueryDataCreateUserIdCommand<TEntity> request, CancellationToken cancellationToken)
    {
        var parameter = Expression.Parameter(typeof(TEntity), "x");
        var property = Expression.Property(parameter, "Id");
        var lambda = Expression.Lambda<Func<TEntity, bool>>(
            Expression.Equal(property, Expression.Constant(request.TEntityId)),
            parameter
        );

        var userId = await _databaseContext.Set<TEntity>()
            .Where(lambda)
            .Select(x => x.CreateUserId)
            .FirstOrDefaultAsync(cancellationToken);


        return new QueryDataCreateUserIdCommandResponse { UserId = userId };
    }
}

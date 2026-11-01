using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Auth.Queries;
using MoAI.Auth.Queries.Responses;
using MoAI.Auth.Services;
using MoAI.Database;
using MoAI.Infra.Models;

namespace MoAI.Auth.Queries;

/// <summary>
/// <inheritdoc cref="QueryTwoFactorStatusQuery"/>.
/// </summary>
public class QueryTwoFactorStatusQueryHandler : IRequestHandler<QueryTwoFactorStatusQuery, QueryTwoFactorStatusQueryResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ILoginSecurityService _loginSecurityService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryTwoFactorStatusQueryHandler"/> class.
    /// </summary>
    public QueryTwoFactorStatusQueryHandler(DatabaseContext databaseContext, ILoginSecurityService loginSecurityService)
    {
        _databaseContext = databaseContext;
        _loginSecurityService = loginSecurityService;
    }

    /// <inheritdoc/>
    public async Task<QueryTwoFactorStatusQueryResponse> Handle(QueryTwoFactorStatusQuery request, CancellationToken cancellationToken)
    {
        var enabled = await _databaseContext.Users
            .Where(u => u.Id == request.ContextUserId && u.IsDeleted == 0)
            .Select(u => u.IsTotpEnabled)
            .FirstOrDefaultAsync(cancellationToken);

        return new QueryTwoFactorStatusQueryResponse
        {
            Enabled = enabled,
            Forced = await _loginSecurityService.IsForced2faAsync(cancellationToken)
        };
    }
}

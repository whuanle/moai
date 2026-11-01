using MediatR;
using MoAI.Org.Queries;
using MoAI.Org.Queries.Responses;
using MoAI.Org.Services;

namespace MoAI.Org.Handlers;

/// <summary>
/// <inheritdoc cref="QueryOrgGrantUsersCommand"/>
/// </summary>
public class QueryOrgGrantUsersCommandHandler : IRequestHandler<QueryOrgGrantUsersCommand, QueryOrgGrantUsersCommandResponse>
{
    private readonly IOrgGrantService _orgGrantService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryOrgGrantUsersCommandHandler"/> class.
    /// </summary>
    /// <param name="orgGrantService">组织授权领域服务.</param>
    public QueryOrgGrantUsersCommandHandler(IOrgGrantService orgGrantService)
    {
        _orgGrantService = orgGrantService;
    }

    /// <inheritdoc/>
    public async Task<QueryOrgGrantUsersCommandResponse> Handle(QueryOrgGrantUsersCommand request, CancellationToken cancellationToken)
    {
        var resourceType = OrgGrantGuard.ParseResourceType(request.ResourceType);
        var userIds = await _orgGrantService.ListAllowedUserIdsAsync(resourceType, request.ResourceId, cancellationToken);
        return new QueryOrgGrantUsersCommandResponse { UserIds = userIds.ToList() };
    }
}

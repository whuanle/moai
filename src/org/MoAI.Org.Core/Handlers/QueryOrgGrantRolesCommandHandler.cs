using MediatR;
using MoAI.Org.Queries;
using MoAI.Org.Queries.Responses;
using MoAI.Org.Services;

namespace MoAI.Org.Handlers;

/// <summary>
/// <inheritdoc cref="QueryOrgGrantRolesCommand"/>
/// </summary>
public class QueryOrgGrantRolesCommandHandler : IRequestHandler<QueryOrgGrantRolesCommand, QueryOrgGrantRolesCommandResponse>
{
    private readonly IOrgGrantService _orgGrantService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryOrgGrantRolesCommandHandler"/> class.
    /// </summary>
    /// <param name="orgGrantService">组织授权领域服务.</param>
    public QueryOrgGrantRolesCommandHandler(IOrgGrantService orgGrantService)
    {
        _orgGrantService = orgGrantService;
    }

    /// <inheritdoc/>
    public async Task<QueryOrgGrantRolesCommandResponse> Handle(QueryOrgGrantRolesCommand request, CancellationToken cancellationToken)
    {
        var resourceType = OrgGrantGuard.ParseResourceType(request.ResourceType);
        var items = await _orgGrantService.ListAllowedRolesAsync(resourceType, request.ResourceId, cancellationToken);
        return new QueryOrgGrantRolesCommandResponse { Items = items.ToList() };
    }
}

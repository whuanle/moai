using MediatR;
using MoAI.Org.Queries;
using MoAI.Org.Queries.Responses;
using MoAI.Org.Services;

namespace MoAI.Org.Handlers;

/// <summary>
/// <inheritdoc cref="QueryOrgGrantsCommand"/>
/// </summary>
public class QueryOrgGrantsCommandHandler : IRequestHandler<QueryOrgGrantsCommand, QueryOrgGrantsCommandResponse>
{
    private readonly IOrgGrantService _orgGrantService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryOrgGrantsCommandHandler"/> class.
    /// </summary>
    /// <param name="orgGrantService">组织授权领域服务.</param>
    public QueryOrgGrantsCommandHandler(IOrgGrantService orgGrantService)
    {
        _orgGrantService = orgGrantService;
    }

    /// <inheritdoc/>
    public async Task<QueryOrgGrantsCommandResponse> Handle(QueryOrgGrantsCommand request, CancellationToken cancellationToken)
    {
        var resourceType = OrgGrantGuard.ParseResourceType(request.ResourceType);
        var items = await _orgGrantService.GetGrantsAsync(resourceType, request.ResourceId, cancellationToken);
        return new QueryOrgGrantsCommandResponse { Items = items.ToList() };
    }
}

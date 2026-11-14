using MediatR;
using MoAI.Org.Queries;
using MoAI.Org.Queries.Responses;
using MoAI.Org.Services;

namespace MoAI.Org.Handlers;

/// <summary>
/// <inheritdoc cref="CheckOrgGrantCommand"/>
/// </summary>
public class CheckOrgGrantCommandHandler : IRequestHandler<CheckOrgGrantCommand, CheckOrgGrantCommandResponse>
{
    private readonly IOrgGrantService _orgGrantService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckOrgGrantCommandHandler"/> class.
    /// </summary>
    /// <param name="orgGrantService">组织授权领域服务.</param>
    public CheckOrgGrantCommandHandler(IOrgGrantService orgGrantService)
    {
        _orgGrantService = orgGrantService;
    }

    /// <inheritdoc/>
    public async Task<CheckOrgGrantCommandResponse> Handle(CheckOrgGrantCommand request, CancellationToken cancellationToken)
    {
        var resourceType = OrgGrantGuard.ParseResourceType(request.ResourceType);
        var allowed = await _orgGrantService.IsAllowedAsync(resourceType, request.ResourceId, request.UserId, cancellationToken);
        return new CheckOrgGrantCommandResponse { Allowed = allowed };
    }
}

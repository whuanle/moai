using MediatR;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Org.Commands;
using MoAI.Org.Queries.Responses;
using MoAI.Org.Services;

namespace MoAI.Org.Handlers;

/// <summary>
/// <inheritdoc cref="SetOrgGrantsCommand"/>
/// </summary>
public class SetOrgGrantsCommandHandler : IRequestHandler<SetOrgGrantsCommand, SetOrgGrantsCommandResponse>
{
    private readonly IOrgGrantService _orgGrantService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SetOrgGrantsCommandHandler"/> class.
    /// </summary>
    /// <param name="orgGrantService">组织授权领域服务.</param>
    public SetOrgGrantsCommandHandler(IOrgGrantService orgGrantService)
    {
        _orgGrantService = orgGrantService;
    }

    /// <inheritdoc/>
    public async Task<SetOrgGrantsCommandResponse> Handle(SetOrgGrantsCommand request, CancellationToken cancellationToken)
    {
        var resourceType = OrgGrantGuard.ParseResourceType(request.ResourceType);
        if (string.IsNullOrWhiteSpace(request.ResourceId) || request.ResourceId.Length > 64)
        {
            throw new BusinessException("资源主键不合法.") { StatusCode = 400 };
        }

        await _orgGrantService.ReplaceGrantsAsync(resourceType, request.ResourceId, request.Grants, cancellationToken);
        return new SetOrgGrantsCommandResponse { Count = request.Grants.Count };
    }
}

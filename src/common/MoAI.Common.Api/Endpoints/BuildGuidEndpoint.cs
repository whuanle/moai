using FastEndpoints;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Common.Endpoints;

/// <summary>
/// 分配一个唯一的 id.
/// </summary>
[HttpGet($"{ApiPrefix.Prefix}/build_guid")]
public class BuildGuidEndpoint : EndpointWithoutRequest<SimpleGuid>
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="BuildGuidEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    public BuildGuidEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public override async Task<SimpleGuid> ExecuteAsync(CancellationToken ct)
    {
        await Task.CompletedTask;
        return Guid.CreateVersion7();
    }
}

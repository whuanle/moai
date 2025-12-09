using FastEndpoints;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Wiki.Wikis.Commands;

namespace MoAI.Wiki.Wikis.Endpoints;

/// <summary>
/// 创建知识库.
/// </summary>
[HttpPost($"{ApiPrefix.Prefix}/create")]
public class CreateWikiEndpoint : Endpoint<CreateWikiCommand, SimpleInt>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateWikiEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public CreateWikiEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<SimpleInt> ExecuteAsync(CreateWikiCommand req, CancellationToken ct)
    {
        return await _mediator.Send(req);
    }
}

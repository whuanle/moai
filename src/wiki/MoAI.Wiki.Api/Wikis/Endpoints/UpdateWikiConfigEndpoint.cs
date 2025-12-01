using FastEndpoints;
using MediatR;
using MoAI.AiModel.Queries;
using MoAI.Common.Queries;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Wiki.Wikis.Commands;
using MoAI.Wiki.Wikis.Queries;

namespace MoAI.Wiki.Wikis.Endpoints;

/// <summary>
/// 更新知识库配置.
/// </summary>
[HttpPost($"{ApiPrefix.Prefix}/update_wiki_config")]
public class UpdateWikiConfigEndpoint : Endpoint<UpdateWikiConfigCommand, EmptyCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateWikiConfigEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public UpdateWikiConfigEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<EmptyCommandResponse> ExecuteAsync(UpdateWikiConfigCommand req, CancellationToken ct)
    {
        var isCreator = await _mediator.Send(new QueryWikiCreatorCommand
        {
            WikiId = req.WikiId
        });

        if (isCreator.CreatorId == _userContext.UserId)
        {
            return await _mediator.Send(req);
        }

        throw new BusinessException("没有操作权限.") { StatusCode = 403 };
    }
}

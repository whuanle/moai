using FastEndpoints;
using MediatR;
using MoAI.Common.Queries;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Wiki.Wikis.Commands;
using MoAI.Wiki.Wikis.Queries;

namespace MoAI.Wiki.Wikis.Endpoints;

/// <summary>
/// 删除知识库.
/// </summary>
[HttpDelete($"{ApiPrefix.Prefix}/delete_wiki")]
public class DeleteWikiEndpoint : Endpoint<DeleteWikiCommand, EmptyCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteWikiEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public DeleteWikiEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<EmptyCommandResponse> ExecuteAsync(DeleteWikiCommand req, CancellationToken ct)
    {
        var isCreator = await _mediator.Send(new QueryWikiCreatorCommand
        {
            WikiId = req.WikiId
        });

        if (!isCreator.WikiIsExist)
        {
            throw new BusinessException("未找到知识库.") { StatusCode = 404 };
        }

        if (isCreator.CreatorId != _userContext.UserId)
        {
            throw new BusinessException("知识库创建人才能操作.") { StatusCode = 404 };
        }

        return await _mediator.Send(req);
    }
}

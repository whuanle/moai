using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Wiki.DocumentManager.Handlers;
using MoAI.Wiki.Documents.Commands;
using MoAI.Wiki.Wikis.Queries;

namespace MoAI.Wiki.Documents.Endpoints;

/// <summary>
/// 取消文档向量化任务.
/// </summary>
[HttpPost($"{ApiPrefix.Document}/create_document")]
public class CancalWikiDocumentTaskEndpoint : Endpoint<CancalWikiDocumentTaskCommand, EmptyCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="CancalWikiDocumentTaskEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public CancalWikiDocumentTaskEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<EmptyCommandResponse> ExecuteAsync(CancalWikiDocumentTaskCommand req, CancellationToken ct)
    {
        var userIsWikiUser = await _mediator.Send(new QueryUserIsWikiUserCommand
        {
            ContextUserId = _userContext.UserId,
            WikiId = req.WikiId
        });

        if (!userIsWikiUser.IsWikiUser)
        {
            throw new BusinessException("没有操作权限.") { StatusCode = 403 };
        }

        return await _mediator.Send(req);
    }
}
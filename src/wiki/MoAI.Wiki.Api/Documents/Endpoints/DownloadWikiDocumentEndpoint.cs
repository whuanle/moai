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
/// 获取文档下载地址.
/// </summary>
[HttpPost($"{ApiPrefix.Document}/download_document")]
public class DownloadWikiDocumentEndpoint : Endpoint<DownloadWikiDocumentCommand, SimpleString>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="DownloadWikiDocumentEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public DownloadWikiDocumentEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<SimpleString> ExecuteAsync(DownloadWikiDocumentCommand req, CancellationToken ct)
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

using FastEndpoints;
using MediatR;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Wiki.DocumentEmbedding.Commands;
using MoAI.Wiki.DocumentEmbedding.Models;
using MoAI.Wiki.DocumentEmbeddings.Queries;
using MoAI.Wiki.Wikis.Queries;

namespace MoAI.Wiki.DocumentEmbeddings.Endpoints;

/// <summary>
/// 切割文档.
/// </summary>
[HttpPost($"{ApiPrefix.Document}/text_partition_document")]
public class WikiDocumentTextPartitionPrevieEndpoint : Endpoint<WikiDocumentTextPartitionPreviewCommand, WikiDocumentTextPartitionPreviewCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="WikiDocumentTextPartitionPrevieEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public WikiDocumentTextPartitionPrevieEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<WikiDocumentTextPartitionPreviewCommandResponse> ExecuteAsync(WikiDocumentTextPartitionPreviewCommand req, CancellationToken ct)
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

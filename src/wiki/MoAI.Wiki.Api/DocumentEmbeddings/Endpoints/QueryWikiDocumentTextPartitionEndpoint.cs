using FastEndpoints;
using MediatR;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Wiki.DocumentEmbedding.Models;
using MoAI.Wiki.DocumentEmbeddings.Queries;
using MoAI.Wiki.Wikis.Queries;

namespace MoAI.Wiki.DocumentEmbeddings.Endpoints;

/// <summary>
/// 获取切割文档.
/// </summary>
[HttpPost($"{ApiPrefix.Document}/get_partition_document")]
public class QueryWikiDocumentTextPartitionEndpoint : Endpoint<QueryWikiDocumentTextPartitionCommand, QueryWikiDocumentTextPartitionCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWikiDocumentTextPartitionEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public QueryWikiDocumentTextPartitionEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<QueryWikiDocumentTextPartitionCommandResponse> ExecuteAsync(QueryWikiDocumentTextPartitionCommand req, CancellationToken ct)
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
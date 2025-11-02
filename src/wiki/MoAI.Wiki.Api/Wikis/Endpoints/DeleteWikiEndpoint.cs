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

        // 系统知识库无论是谁创建的，只能由 root 用户删除，
        if (isCreator.IsSystem)
        {
            var isAdmin = await _mediator.Send(new QueryUserIsAdminCommand
            {
                ContextUserId = _userContext.UserId
            });

            if (!isAdmin.IsRoot)
            {
                throw new BusinessException("只有超级管理员可以删除系统知识库.") { StatusCode = 403 };
            }
        }
        else
        {
            if (isCreator.CreatorId != _userContext.UserId)
            {
                throw new BusinessException("未找到知识库.") { StatusCode = 404 };
            }
        }

        return await _mediator.Send(req);
    }
}

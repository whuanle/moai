using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Wiki.Wikis.Commands;
using MoAI.Wiki.Wikis.Queries;

namespace MoAI.Wiki.Controllers;

/// <summary>
/// 管理知识库.
/// </summary>
[ApiController]
[Route("/wiki/manager")]
[Authorize]
public partial class WikiManagerController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="WikiManagerController"/> class.
    /// 初始化一个 <see cref="WikiController"/> 的新实例.
    /// </summary>
    /// <param name="mediator">MediatR 的实例.</param>
    /// <param name="userContext">当前请求的用户上下文.</param>
    public WikiManagerController(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <summary>
    /// 删除知识库.
    /// </summary>
    /// <param name="req">删除知识库的命令对象, 包含要删除的 WikiId.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/> 表示操作结果.</returns>
    [HttpDelete("delete_wiki")]
    public async Task<EmptyCommandResponse> Delete([FromBody] DeleteWikiCommand req, CancellationToken ct = default)
    {
        await CheckIsCreatorAsync(req.WikiId, ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 更新知识库配置.
    /// </summary>
    /// <param name="req">更新知识库配置的命令对象, 包含 WikiId 与配置内容.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/> 表示操作结果.</returns>
    [HttpPost("update_wiki_config")]
    public async Task<EmptyCommandResponse> UpdateWikiConfig([FromBody] UpdateWikiConfigCommand req, CancellationToken ct = default)
    {
        await CheckIsCreatorAsync(req.WikiId, ct);

        return await _mediator.Send(request: req, ct);
    }

    private async Task CheckIsCreatorAsync(int wikiId, CancellationToken ct)
    {
        var isCreator = await _mediator.Send(new QueryWikiCreatorCommand { WikiId = wikiId, ContextUserId = _userContext.UserId }, ct);

        if (!isCreator.IsExist)
        {
            throw new BusinessException("未找到知识库.") { StatusCode = 404 };
        }

        if (isCreator.IsTeam == false && isCreator.CreatorId != _userContext.UserId)
        {
            throw new BusinessException("知识库创建人才能操作.") { StatusCode = 409 };
        }

        if (isCreator.IsTeam == true && isCreator.TeamRole < Team.Models.TeamRole.Admin)
        {
            throw new BusinessException("团队管理员才能操作.") { StatusCode = 409 };
        }
    }
}

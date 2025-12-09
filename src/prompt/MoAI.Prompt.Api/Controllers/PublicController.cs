using MediatR;
using Microsoft.AspNetCore.Mvc;
using MoAI.Infra.Models;
using MoAI.Prompt.Queries;
using MoAI.Prompt.Queries.Responses;

namespace MoAI.Prompt.Controllers;

/// <summary>
/// 提示词.
/// </summary>
[ApiController]
[Route("/public/plugin")]
public class PublicController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="PublicController"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public PublicController(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <summary>
    /// 查询提示词分类列表.
    /// </summary>
    /// <param name="ct">取消令牌.</param>
    /// <returns>查询结果, 返回 <see cref="QueryePromptClassCommandResponse"/>.</returns>
    [HttpGet("class_list")]
    public async Task<QueryePromptClassCommandResponse> QueryAsync(CancellationToken ct = default)
    {
        return await _mediator.Send(new QueryePromptClassCommand(), ct);
    }
}
using MediatR;
using Microsoft.AspNetCore.Mvc;
using MoAI.Infra.Models;
using MoAI.Plugin.Classify.Queries;
using MoAI.Plugin.Classify.Queries.Responses;

namespace MoAI.Plugin.Controllers;

/// <summary>
/// 插件分类.
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
    /// 获取分类列表.
    /// </summary>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryPluginClassifyListCommandResponse"/>，包含分类列表数据.</returns>
    [HttpGet("classify_list")]
    public async Task<QueryPluginClassifyListCommandResponse> QueryList(CancellationToken ct = default)
    {
        return await _mediator.Send(new QueryPluginClassifyListCommand(), ct);
    }
}
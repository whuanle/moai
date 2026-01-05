using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using MoAI.Infra.Models;
using MoAI.Plugin.Classify.Queries;
using MoAI.Plugin.Classify.Queries.Responses;
using MoAI.Plugin.Public.Queries;

namespace MoAI.Plugin.Controllers;

/// <summary>
/// 插件分类.
/// </summary>
[ApiController]
[Route("/plugin")]
[EndpointGroupName("plugin")]
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
    /// 获取插件分类列表.
    /// </summary>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryPluginClassifyListCommandResponse"/>，包含分类列表数据.</returns>
    [HttpGet("classify_list")]
    public async Task<QueryPluginClassifyListCommandResponse> ClassifyList(CancellationToken ct = default)
    {
        return await _mediator.Send(new QueryPluginClassifyListCommand(), ct);
    }

    /// <summary>
    /// 获取所有公开使用的插件.
    /// </summary>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryPluginClassifyListCommandResponse"/>，包含分类列表数据.</returns>
    [HttpGet("plugin_list")]
    public async Task<QueryPublicPluginListCommandResponse> PluginList(CancellationToken ct = default)
    {
        return await _mediator.Send(new QueryPublicPluginListCommand(), ct);
    }
}
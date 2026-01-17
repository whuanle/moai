using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using MoAI.App.Apps.CommonApp.Queries;
using MoAI.App.Apps.CommonApp.Responses;
using MoAI.App.Classify.Queries;
using MoAI.App.Classify.Queries.Responses;
using MoAI.Infra.Models;

namespace MoAI.App.Controllers;

/// <summary>
/// 应用商店.
/// </summary>
[ApiController]
[Route("/app/store")]
[EndpointGroupName("app_store")]
public class AppStoreController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppStoreController"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public AppStoreController(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <summary>
    /// 获取分类列表.
    /// </summary>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryAppClassifyListCommandResponse"/>，包含分类列表数据.</returns>
    [HttpGet("classify_list")]
    public async Task<QueryAppClassifyListCommandResponse> QueryList(CancellationToken ct = default)
    {
        return await _mediator.Send(
            new QueryAppClassifyListCommand
            {
                ContextUserId = _userContext.UserId,
                ContextUserType = _userContext.UserType
            },
            ct);
    }

    /// <summary>
    /// 获取用户可访问的应用列表（公开应用 + 用户加入的团队的应用），可以看到各类应用.
    /// </summary>
    /// <param name="req">查询请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryAccessibleAppListCommandResponse"/>，包含用户可访问的应用列表.</returns>
    [HttpPost("accessible_list")]
    public async Task<QueryAccessibleAppListCommandResponse> QueryAccessibleAppList([FromBody] QueryAccessibleAppListCommand req, CancellationToken ct = default)
    {
        return await _mediator.Send(req, ct);
    }
}

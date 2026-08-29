#pragma warning disable CA1822 // 将成员标记为 static

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoAI.Common.Queries;
using MoAI.Common.Queries.Response;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.Login.Queries.Responses;

#pragma warning disable SA1009

namespace MoAI.Common.Controllers;

/// <summary>
/// 公共接口.
/// </summary>
[ApiController]
[Microsoft.AspNetCore.Mvc.Route("/common")]
public class CommonController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommonController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例，用于发送命令/查询.</param>
    /// <param name="userContext">当前用户上下文.</param>
    public CommonController(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <summary>
    /// 分配一个唯一的 id.
    /// </summary>
    /// <param name="ct">CancellationToken，用于取消操作.</param>
    /// <returns>返回生成的 <see cref="SimpleGuid"/>.</returns>
    [HttpGet("build_guid")]
    public async Task<SimpleGuid> BuildGuid(CancellationToken ct)
    {
        await Task.CompletedTask;
        return Guid.CreateVersion7();
    }

    /// <summary>
    /// 查询用户基本信息.
    /// </summary>
    /// <param name="ct">CancellationToken，用于取消操作.</param>
    /// <returns>返回 <see cref="UserStateInfo"/>，包含用户状态信息.</returns>
    [HttpGet("userinfo")]
    public Task<UserStateInfo> QueryUserInfo(CancellationToken ct)
    {
        var cmd = new QueryUserViewUserInfoCommand() { ContextUserId = _userContext.UserId };
        return _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 获取服务器信息.
    /// </summary>
    /// <param name="ct">CancellationToken，用于取消操作.</param>
    /// <returns>返回 <see cref="QueryServerInfoCommandResponse"/>，包含服务器信息.</returns>
    [HttpGet("serverinfo")]
    [AllowAnonymous]
    public Task<QueryServerInfoCommandResponse> ServerInfo(CancellationToken ct)
    {
        return _mediator.Send(new QueryServerInfoCommand { }, ct);
    }
}
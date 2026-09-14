using MediatR;
using Microsoft.AspNetCore.Mvc;
using MoAI.App.Commands;
using MoAI.App.Queries;
using MoAI.App.Queries.Responses;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.App.Controllers;

/// <summary>
/// 应用接入接口：团队下创建的 key，授权其可访问哪些外部应用；增删改查需要团队 Admin 及以上角色.
/// </summary>
[ApiController]
[Route("/access-app")]
public class AccessAppController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserContextProvider _userContextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="AccessAppController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    public AccessAppController(IMediator mediator, IUserContextProvider userContextProvider)
    {
        _mediator = mediator;
        _userContextProvider = userContextProvider;
    }

    /// <summary>
    /// 查询团队下的应用接入列表，不回显 key 原文（仅前缀），需要团队 Admin 及以上角色.
    /// </summary>
    /// <param name="req">查询参数.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryAccessAppsCommandResponse"/>.</returns>
    [HttpGet("list")]
    public Task<QueryAccessAppsCommandResponse> QueryAccessApps([FromQuery] QueryAccessAppsCommand req, CancellationToken ct)
    {
        _userContextProvider.SetUserContext(req);
        return _mediator.Send(req, ct);
    }

    /// <summary>
    /// 创建应用接入，key 原文仅在创建响应返回一次；需要团队 Admin 及以上角色.
    /// </summary>
    /// <param name="req">创建请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="CreateAccessAppCommandResponse"/>.</returns>
    [HttpPost]
    public Task<CreateAccessAppCommandResponse> CreateAccessApp([FromBody] CreateAccessAppCommand req, CancellationToken ct)
    {
        _userContextProvider.SetUserContext(req);
        return _mediator.Send(req, ct);
    }

    /// <summary>
    /// 更新应用接入（名称、描述、授权外部应用；key 不可改），需要团队 Admin 及以上角色.
    /// </summary>
    /// <param name="id">接入 id.</param>
    /// <param name="req">更新请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPut("{id:guid}")]
    public async Task<EmptyCommandResponse> UpdateAccessApp([FromRoute] Guid id, [FromBody] UpdateAccessAppCommand req, CancellationToken ct)
    {
        var cmd = new UpdateAccessAppCommand
        {
            AccessAppId = id,
            Name = req.Name,
            Description = req.Description,
            AppIds = req.AppIds,
        };
        _userContextProvider.SetUserContext(cmd);
        return await _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 删除应用接入（软删除），需要团队 Admin 及以上角色.
    /// </summary>
    /// <param name="id">接入 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpDelete("{id:guid}")]
    public async Task<EmptyCommandResponse> DeleteAccessApp([FromRoute] Guid id, CancellationToken ct)
    {
        var cmd = new DeleteAccessAppCommand { AccessAppId = id };
        _userContextProvider.SetUserContext(cmd);
        return await _mediator.Send(cmd, ct);
    }
}

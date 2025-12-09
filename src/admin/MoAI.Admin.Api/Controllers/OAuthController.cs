using MediatR;
using Microsoft.AspNetCore.Mvc;
using MoAI.Admin.OAuth.Queries.Responses;
using MoAI.Common.Queries;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Login.Commands;
using MoAI.Login.Queries;

namespace MoAI.Admin.Controllers;

/// <summary>
/// OAuth 配置.
/// </summary>
[ApiController]
[Microsoft.AspNetCore.Mvc.Route("/admin/oauth")]
public class OAuthController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="OAuthController"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public OAuthController(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <summary>
    /// 创建 OAuth2.0 连接配置.
    /// </summary>
    /// <param name="req"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpPost("create")]
    public async Task<EmptyCommandResponse> Create([FromBody] CreateOAuthConnectionCommand req, CancellationToken ct)
    {
        await CheckIsAdminAsync(ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 删除连接配置.
    /// </summary>
    /// <param name="req"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpDelete("delete")]
    public async Task<EmptyCommandResponse> Delete([FromBody] DeleteOAuthConnectionCommand req, CancellationToken ct)
    {
        await CheckIsAdminAsync(ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 获取连接配置内容.
    /// </summary>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpGet("detail_list")]
    public async Task<QueryAllOAuthPrividerDetailCommandResponse> DetailList(CancellationToken ct)
    {
        await CheckIsAdminAsync(ct);

        return await _mediator.Send(new QueryAllOAuthPrividerDetailCommand(), ct);
    }

    /// <summary>
    /// 更新连接配置.
    /// </summary>
    /// <param name="req"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpPut("update")]
    public async Task<EmptyCommandResponse> Update([FromBody] UpdateOAuthConnectionCommand req, CancellationToken ct)
    {
        await CheckIsAdminAsync(ct);

        return await _mediator.Send(req, ct);
    }

    private async Task CheckIsAdminAsync(CancellationToken ct)
    {
        var isAdmin = await _mediator.Send(new QueryUserIsAdminCommand { ContextUserId = _userContext.UserId }, ct);

        if (!isAdmin.IsAdmin)
        {
            throw new BusinessException("没有操作权限.") { StatusCode = 403 };
        }
    }
}
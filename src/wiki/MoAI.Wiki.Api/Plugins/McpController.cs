using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Models;
using MoAI.Wiki.Plugins.Mcp.Commands;
using MoAI.Wiki.Plugins.Mcp.Models;
using MoAI.Wiki.Plugins.Mcp.Queries;
using MoAI.Wiki.Wikis.Queries;

namespace MoAI.Wiki.Plugins;

/// <summary>
/// 知识库 MCP 插件接口.
/// </summary>
[ApiController]
[Route("/wiki/plugin/mcp")]
[EndpointGroupName("wiki")]
public class McpController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpController"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public McpController(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <summary>
    /// 启用禁用知识库 MCP 功能.
    /// </summary>
    /// <param name="req">开启命令.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回操作结果.</returns>
    [HttpPost("enable")]
    public async Task<EmptyCommandResponse> EnableMcp([FromBody] EnableWikiMcpCommand req, CancellationToken ct = default)
    {
        await CheckUserIsMemberAsync(req.WikiId, ct);
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 查询知识库 MCP 配置信息.
    /// </summary>
    /// <param name="req">查询命令.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 MCP 配置信息.</returns>
    [HttpGet("config")]
    public async Task<QueryWikiMcpConfigCommandResponse> QueryConfig([FromQuery] QueryWikiMcpConfigCommand req, CancellationToken ct = default)
    {
        await CheckUserIsMemberAsync(req.WikiId, ct);
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 重置知识库 MCP 密钥.
    /// </summary>
    /// <param name="req">重置命令.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回操作结果.</returns>
    [HttpPost("reset_key")]
    public async Task<EmptyCommandResponse> ResetKey([FromBody] ResetWikiMcpKeyCommand req, CancellationToken ct = default)
    {
        await CheckUserIsMemberAsync(req.WikiId, ct);
        return await _mediator.Send(req, ct);
    }

    private async Task CheckUserIsMemberAsync(int wikiId, CancellationToken ct)
    {
        var userIsWikiUser = await _mediator.Send(
            new QueryUserIsWikiMemberCommand
            {
                ContextUserId = _userContext.UserId,
                WikiId = wikiId
            },
            ct);

        if (userIsWikiUser.TeamRole == TeamRole.None)
        {
            throw new BusinessException("没有操作权限.") { StatusCode = 403 };
        }
    }
}

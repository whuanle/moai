using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.TeamPlugin.Commands;
using MoAI.TeamPlugin.Queries;
using MoAI.TeamPlugin.Queries.Responses;

namespace MoAI.TeamPlugin.Controllers;

/// <summary>
/// 团队插件接口：团队自有插件（创建后归团队）+ 团队可用的系统插件.
/// </summary>
[ApiController]
[Route("/team/{teamId}/plugin")]
public class TeamPluginController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserContextProvider _userContextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="TeamPluginController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例，用于发送命令/查询.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    public TeamPluginController(IMediator mediator, IUserContextProvider userContextProvider)
    {
        _mediator = mediator;
        _userContextProvider = userContextProvider;
    }

    /// <summary>
    /// 查询团队可用插件列表（团队自有插件 + 可用的系统插件）；仅团队成员可访问.
    /// </summary>
    /// <param name="teamId">团队 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryTeamPluginsCommandResponse"/>.</returns>
    [HttpGet("list")]
    public async Task<QueryTeamPluginsCommandResponse> QueryList(long teamId, CancellationToken ct)
    {
        var cmd = new QueryTeamPluginsCommand { TeamId = teamId };
        _userContextProvider.SetUserContext(cmd);
        return await _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 创建团队动态插件实例，需团队 Owner/Admin.
    /// </summary>
    /// <param name="teamId">团队 id.</param>
    /// <param name="req">请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPost("dynamic")]
    public async Task<EmptyCommandResponse> SaveDynamic(long teamId, [FromBody] SaveTeamDynamicPluginCommand req, CancellationToken ct)
    {
        var cmd = new SaveTeamDynamicPluginCommand
        {
            TeamId = teamId,
            InstanceKey = req.InstanceKey,
            TempleteKey = req.TempleteKey,
            Title = req.Title,
            Description = req.Description,
            Config = req.Config,
        };
        _userContextProvider.SetUserContext(cmd);
        return await _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 导入/更新团队 MCP 插件，需团队 Owner/Admin.
    /// </summary>
    /// <param name="teamId">团队 id.</param>
    /// <param name="req">请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回新增插件 <see cref="SimpleGuid"/>.</returns>
    [HttpPost("mcp")]
    public async Task<SimpleGuid> SaveMcp(long teamId, [FromBody] SaveTeamMcpPluginCommand req, CancellationToken ct)
    {
        var cmd = new SaveTeamMcpPluginCommand
        {
            TeamId = teamId,
            PluginId = req.PluginId,
            Name = req.Name,
            Title = req.Title,
            Description = req.Description,
            ServerUrl = req.ServerUrl,
            Header = req.Header,
            Query = req.Query,
        };
        _userContextProvider.SetUserContext(cmd);
        return await _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 导入/更新团队 OpenAPI 插件，需团队 Owner/Admin.
    /// </summary>
    /// <param name="teamId">团队 id.</param>
    /// <param name="req">请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回新增插件 <see cref="SimpleGuid"/>.</returns>
    [HttpPost("openapi")]
    public async Task<SimpleGuid> SaveOpenApi(long teamId, [FromBody] SaveTeamOpenApiPluginCommand req, CancellationToken ct)
    {
        var cmd = new SaveTeamOpenApiPluginCommand
        {
            TeamId = teamId,
            PluginId = req.PluginId,
            FileId = req.FileId,
            FileName = req.FileName,
            Name = req.Name,
            Title = req.Title,
            Description = req.Description,
        };
        _userContextProvider.SetUserContext(cmd);
        return await _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 删除团队插件，需团队 Owner/Admin.
    /// </summary>
    /// <param name="teamId">团队 id.</param>
    /// <param name="pluginId">插件记录 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpDelete("{pluginId}")]
    public async Task<EmptyCommandResponse> Delete(long teamId, Guid pluginId, CancellationToken ct)
    {
        var cmd = new DeleteTeamPluginCommand { TeamId = teamId, PluginId = pluginId };
        _userContextProvider.SetUserContext(cmd);
        return await _mediator.Send(cmd, ct);
    }
}

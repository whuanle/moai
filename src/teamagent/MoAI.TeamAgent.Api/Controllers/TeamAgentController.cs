using System;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.TeamAgent.Commands;
using MoAI.TeamAgent.Queries;
using MoAI.TeamAgent.Queries.Responses;

namespace MoAI.TeamAgent.Controllers;

/// <summary>
/// 团队智能体接口：主智能体 + 多个子智能体的编排定义资产管理；
/// 团队成员可查看，仅团队 Admin 及以上可管理（目标保护在 Handler 校验）.
/// </summary>
[ApiController]
[Route("/team-agent")]
public class TeamAgentController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserContextProvider _userContextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="TeamAgentController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例，用于发送命令/查询.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    public TeamAgentController(IMediator mediator, IUserContextProvider userContextProvider)
    {
        _mediator = mediator;
        _userContextProvider = userContextProvider;
    }

    /// <summary>
    /// 查询团队智能体列表，仅团队成员可访问.
    /// </summary>
    /// <param name="req">查询参数.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryTeamAgentListCommandResponse"/>.</returns>
    [HttpGet("list")]
    public Task<QueryTeamAgentListCommandResponse> QueryList([FromQuery] QueryTeamAgentListCommand req, CancellationToken ct)
    {
        _userContextProvider.SetUserContext(req);
        return _mediator.Send(req, ct);
    }

    /// <summary>
    /// 查询智能体详情（含子智能体定义），仅团队成员可访问.
    /// </summary>
    /// <param name="id">智能体 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryTeamAgentDetailCommandResponse"/>.</returns>
    [HttpGet("{id}")]
    public Task<QueryTeamAgentDetailCommandResponse> QueryDetail(Guid id, CancellationToken ct)
    {
        var cmd = new QueryTeamAgentDetailCommand { AgentId = id };
        _userContextProvider.SetUserContext(cmd);
        return _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 创建团队智能体，需要团队 Admin 及以上角色.
    /// </summary>
    /// <param name="req">创建请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回智能体 id <see cref="SimpleGuid"/>.</returns>
    [HttpPost]
    public Task<SimpleGuid> CreateTeamAgent([FromBody] CreateTeamAgentCommand req, CancellationToken ct)
    {
        return _mediator.Send(req, ct);
    }

    /// <summary>
    /// 为全部存量团队补种内置智能体（规划师/构建师，按名称幂等），仅平台管理员.
    /// </summary>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回本次种入数量 <see cref="SimpleInt"/>.</returns>
    [HttpPost("seed-builtin")]
    public Task<SimpleInt> SeedBuiltin(CancellationToken ct)
    {
        var cmd = new SeedTeamAgentBuiltinCommand();
        _userContextProvider.SetUserContext(cmd);
        return _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 更新团队智能体（整文档替换，含子智能体列表），需要团队 Admin 及以上角色.
    /// </summary>
    /// <param name="id">智能体 id.</param>
    /// <param name="req">更新请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPut("{id}")]
    public async Task<EmptyCommandResponse> UpdateTeamAgent(Guid id, [FromBody] UpdateTeamAgentCommand req, CancellationToken ct)
    {
        var cmd = new UpdateTeamAgentCommand
        {
            AgentId = id,
            Name = req.Name,
            Description = req.Description,
            Enabled = req.Enabled,
            MainPrompt = req.MainPrompt,
            MainPluginIds = req.MainPluginIds,
            MainWikiIds = req.MainWikiIds,
            MainGraphIds = req.MainGraphIds,
            Subs = req.Subs,
        };
        _userContextProvider.SetUserContext(cmd);
        return await _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 设置智能体头像；objectKey 须为已完成上传并登记的文件，需要团队 Admin 及以上角色.
    /// </summary>
    /// <param name="id">智能体 id.</param>
    /// <param name="req">头像请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPost("{id}/avatar")]
    public Task<EmptyCommandResponse> UpdateTeamAgentAvatar(Guid id, [FromBody] UpdateTeamAgentAvatarCommand req, CancellationToken ct)
    {
        var cmd = new UpdateTeamAgentAvatarCommand { AgentId = id, ObjectKey = req.ObjectKey };
        _userContextProvider.SetUserContext(cmd);
        return _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 删除团队智能体（软删除），需要团队 Admin 及以上角色.
    /// </summary>
    /// <param name="id">智能体 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpDelete("{id}")]
    public Task<EmptyCommandResponse> DeleteTeamAgent(Guid id, CancellationToken ct)
    {
        var cmd = new DeleteTeamAgentCommand { AgentId = id };
        _userContextProvider.SetUserContext(cmd);
        return _mediator.Send(cmd, ct);
    }
}

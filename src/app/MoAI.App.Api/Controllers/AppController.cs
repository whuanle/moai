using MediatR;
using Microsoft.AspNetCore.Mvc;
using MoAI.App.Commands;
using MoAI.App.Queries;
using MoAI.App.Queries.Responses;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.App.Controllers;

/// <summary>
/// 团队应用接口；应用是团队下的产物，创建与配置需要团队 Admin 及以上角色，查看仅需团队成员.
/// </summary>
[ApiController]
[Route("/app")]
public class AppController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserContextProvider _userContextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例，用于发送命令/查询.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    public AppController(IMediator mediator, IUserContextProvider userContextProvider)
    {
        _mediator = mediator;
        _userContextProvider = userContextProvider;
    }

    /// <summary>
    /// 创建应用（Agent 应用/流程应用），需要团队 Admin 及以上角色；可选携带头像 objectKey 与「允许外部使用」开关.
    /// </summary>
    /// <param name="req">创建请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回应用 <see cref="SimpleGuid"/>.</returns>
    [HttpPost]
    public Task<SimpleGuid> CreateApp([FromBody] CreateAppCommand req, CancellationToken ct)
    {
        _userContextProvider.SetUserContext(req);
        return _mediator.Send(req, ct);
    }

    /// <summary>
    /// 更新应用基础信息（名称、描述、允许外部使用），需要团队 Admin 及以上角色；应用类型不可修改.
    /// </summary>
    /// <param name="id">应用 id.</param>
    /// <param name="req">更新请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPut("{id:guid}")]
    public async Task<EmptyCommandResponse> UpdateApp([FromRoute] Guid id, [FromBody] UpdateAppCommand req, CancellationToken ct)
    {
        var cmd = new UpdateAppCommand
        {
            AppId = id,
            Name = req.Name,
            Description = req.Description,
            EnableForeign = req.EnableForeign
        };
        _userContextProvider.SetUserContext(cmd);
        return await _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 设置应用头像，需要团队 Admin 及以上角色；objectKey 须为已完成上传并登记的文件.
    /// </summary>
    /// <param name="id">应用 id.</param>
    /// <param name="req">头像请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPost("{id:guid}/avatar")]
    public async Task<EmptyCommandResponse> UpdateAppAvatar([FromRoute] Guid id, [FromBody] UpdateAppAvatarCommand req, CancellationToken ct)
    {
        var cmd = new UpdateAppAvatarCommand { AppId = id, ObjectKey = req.ObjectKey };
        _userContextProvider.SetUserContext(cmd);
        return await _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 查询 Agent 应用配置（对话模型、允许使用的插件、知识库与系统提示词），仅团队成员可访问；未保存过配置时返回空配置.
    /// </summary>
    /// <param name="id">应用 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryAppAgentConfigCommandResponse"/>.</returns>
    [HttpGet("{id:guid}/agent-config")]
    public Task<QueryAppAgentConfigCommandResponse> QueryAppAgentConfig([FromRoute] Guid id, CancellationToken ct)
    {
        var cmd = new QueryAppAgentConfigCommand { AppId = id };
        _userContextProvider.SetUserContext(cmd);
        return _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 保存 Agent 应用配置（对话模型、允许使用的插件、知识库与系统提示词），需要团队 Admin 及以上角色；
    /// 绑定的模型/插件/知识库必须在该团队有权使用的范围内，越权绑定返回 400.
    /// </summary>
    /// <param name="id">应用 id.</param>
    /// <param name="req">配置请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPut("{id:guid}/agent-config")]
    public async Task<EmptyCommandResponse> SaveAppAgentConfig([FromRoute] Guid id, [FromBody] SaveAppAgentConfigCommand req, CancellationToken ct)
    {
        var cmd = new SaveAppAgentConfigCommand
        {
            AppId = id,
            ModelId = req.ModelId,
            Prompt = req.Prompt,
            WikiIds = req.WikiIds,
            Plugins = req.Plugins
        };
        _userContextProvider.SetUserContext(cmd);
        return await _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 查询团队下的应用列表，仅团队成员可访问.
    /// </summary>
    /// <param name="req">查询参数.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryAppsCommandResponse"/>.</returns>
    [HttpGet("list")]
    public Task<QueryAppsCommandResponse> QueryApps([FromQuery] QueryAppsCommand req, CancellationToken ct)
    {
        _userContextProvider.SetUserContext(req);
        return _mediator.Send(req, ct);
    }

    /// <summary>
    /// 查询应用详情，仅团队成员可访问.
    /// </summary>
    /// <param name="id">应用 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryAppCommandResponse"/>.</returns>
    [HttpGet("{id:guid}")]
    public Task<QueryAppCommandResponse> QueryApp([FromRoute] Guid id, CancellationToken ct)
    {
        var cmd = new QueryAppCommand { AppId = id };
        _userContextProvider.SetUserContext(cmd);
        return _mediator.Send(cmd, ct);
    }
}

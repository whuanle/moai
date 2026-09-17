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
            IsExternal = req.IsExternal,
            IsAuth = req.IsAuth
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
    /// 发布应用，发布后团队成员可进入应用进行对话；需要团队 Admin 及以上角色，仅 Agent 应用可发布.
    /// </summary>
    /// <param name="id">应用 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPost("{id:guid}/publish")]
    public async Task<EmptyCommandResponse> PublishApp([FromRoute] Guid id, CancellationToken ct)
    {
        var cmd = new PublishAppCommand { AppId = id };
        _userContextProvider.SetUserContext(cmd);
        return await _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 取消发布应用；需要团队 Admin 及以上角色.
    /// </summary>
    /// <param name="id">应用 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPost("{id:guid}/unpublish")]
    public async Task<EmptyCommandResponse> UnpublishApp([FromRoute] Guid id, CancellationToken ct)
    {
        var cmd = new UnpublishAppCommand { AppId = id };
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
            Plugins = req.Plugins,
            OpeningStatement = req.OpeningStatement,
            OpeningStatementEnabled = req.OpeningStatementEnabled,
            ExecutionSettings = req.ExecutionSettings
        };
        _userContextProvider.SetUserContext(cmd);
        return await _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 查询外部应用访问点配置（内部管理视图），需要团队 Admin 及以上角色；未保存过配置时返回默认值.
    /// </summary>
    /// <param name="id">应用 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="AppAccessPointConfigResponse"/>.</returns>
    [HttpGet("{id:guid}/access-point")]
    public Task<AppAccessPointConfigResponse> QueryAppAccessPoint([FromRoute] Guid id, CancellationToken ct)
    {
        var cmd = new QueryAppAccessPointCommand { AppId = id };
        _userContextProvider.SetUserContext(cmd);
        return _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 保存外部应用访问点配置（整体替换），需要团队 Admin 及以上角色，仅外部应用.
    /// </summary>
    /// <param name="id">应用 id.</param>
    /// <param name="req">配置请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPut("{id:guid}/access-point")]
    public async Task<EmptyCommandResponse> SaveAppAccessPoint([FromRoute] Guid id, [FromBody] SaveAppAccessPointCommand req, CancellationToken ct)
    {
        var cmd = new SaveAppAccessPointCommand
        {
            AppId = id,
            Title = req.Title,
            Subtitle = req.Subtitle,
            Placeholder = req.Placeholder,
            PrimaryColor = req.PrimaryColor,
            Position = req.Position,
            LauncherText = req.LauncherText,
            Avatar = req.Avatar,
            PanelWidth = req.PanelWidth,
            PanelHeight = req.PanelHeight,
            DefaultOpen = req.DefaultOpen,
            Enabled = req.Enabled,
        };
        _userContextProvider.SetUserContext(cmd);
        return await _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 创建应用调试会话：仅存 Redis 热态、不落库、不计用量，未发布应用也可调试；需要团队 Admin 及以上角色.
    /// </summary>
    /// <param name="id">应用 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回调试会话 <see cref="SimpleGuid"/>.</returns>
    [HttpPost("{id:guid}/debug/session")]
    public async Task<SimpleGuid> CreateDebugSession([FromRoute] Guid id, CancellationToken ct)
    {
        var cmd = new CreateDebugSessionCommand { AppId = id };
        _userContextProvider.SetUserContext(cmd);
        return await _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 查询团队下的内部应用列表，仅团队成员可访问.
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
    /// 查询团队下的外部应用列表，需要团队 Admin 及以上角色.
    /// </summary>
    /// <param name="req">查询参数.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryAppsCommandResponse"/>.</returns>
    [HttpGet("external/list")]
    public Task<QueryAppsCommandResponse> QueryExternalApps([FromQuery] QueryExternalAppsCommand req, CancellationToken ct)
    {
        _userContextProvider.SetUserContext(req);
        return _mediator.Send(req, ct);
    }

    /// <summary>
    /// 查询平台公开应用（内部且已公开、已发布、未禁用），任意已登录用户可访问.
    /// </summary>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryPublicAppsCommandResponse"/>.</returns>
    [HttpGet("public/list")]
    public Task<QueryPublicAppsCommandResponse> QueryPublicApps(CancellationToken ct)
    {
        var cmd = new QueryPublicAppsCommand();
        _userContextProvider.SetUserContext(cmd);
        return _mediator.Send(cmd, ct);
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

    /// <summary>
    /// 分页查询应用对话日志（全部用户的正式会话，压缩后视图）；需要团队 Admin 及以上角色.
    /// </summary>
    /// <param name="id">应用 id.</param>
    /// <param name="req">查询参数.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryAppLogsCommandResponse"/>.</returns>
    [HttpGet("{id:guid}/logs")]
    public Task<QueryAppLogsCommandResponse> QueryAppLogs([FromRoute] Guid id, [FromQuery] QueryAppLogsCommand req, CancellationToken ct)
    {
        var cmd = new QueryAppLogsCommand
        {
            AppId = id,
            Keyword = req.Keyword,
            UserType = req.UserType,
            From = req.From,
            To = req.To,
            PageNo = req.PageNo,
            PageSize = req.PageSize
        };
        _userContextProvider.SetUserContext(cmd);
        return _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 查询指定会话的消息详情（压缩后视图）；需要团队 Admin 及以上角色.
    /// </summary>
    /// <param name="id">应用 id.</param>
    /// <param name="sessionId">会话 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryAppLogMessagesCommandResponse"/>.</returns>
    [HttpGet("{id:guid}/logs/{sessionId:guid}/messages")]
    public Task<QueryAppLogMessagesCommandResponse> QueryAppLogMessages([FromRoute] Guid id, [FromRoute] Guid sessionId, CancellationToken ct)
    {
        var cmd = new QueryAppLogMessagesCommand { AppId = id, SessionId = sessionId };
        _userContextProvider.SetUserContext(cmd);
        return _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 查询应用用量统计（调用次数与 token，汇总 + 按模型分布；基于聚合用量表，最多滞后约 1 分钟）；需要团队 Admin 及以上角色.
    /// </summary>
    /// <param name="id">应用 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryAppUsageCommandResponse"/>.</returns>
    [HttpGet("{id:guid}/usage")]
    public Task<QueryAppUsageCommandResponse> QueryAppUsage([FromRoute] Guid id, CancellationToken ct)
    {
        var cmd = new QueryAppUsageCommand { AppId = id };
        _userContextProvider.SetUserContext(cmd);
        return _mediator.Send(cmd, ct);
    }
}

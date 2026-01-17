#pragma warning disable CA1031 // 不捕获常规异常类型

using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using MoAI.AI.Models;
using MoAI.App.Apps.CommonApp.Handlers;
using MoAI.App.Apps.CommonApp.Queries;
using MoAI.App.Apps.CommonApp.Responses;
using MoAI.App.Manager.ManagerApp.Models;
using MoAI.App.Manager.ManagerApp.Queries;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using MoAI.Team.Models;
using MoAI.Team.Queries;

namespace MoAI.App.Controllers;

/// <summary>
/// 使用普通应用.
/// </summary>
[ApiController]
[Route("/app/common")]
[EndpointGroupName("app_common")]
public class AppCommonController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;
    private readonly ILogger<TeamAppController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppCommonController"/> class.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public AppCommonController(IMediator mediator, UserContext userContext, ILogger<TeamAppController> logger)
    {
        _mediator = mediator;
        _userContext = userContext;
        _logger = logger;
    }

    /// <summary>
    /// 查询应用简单信息.
    /// </summary>
    /// <param name="req"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpGet("simple_info")]
    public async Task<QueryAppSimpleInfoCommandResponse> QueryAppSimpleInfo([FromQuery] QueryAppSimpleInfoCommand req, CancellationToken ct = default)
    {
        await CheckCanAccessAppAsync(appId: req.AppId, ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 查询应用详细信息.
    /// </summary>
    /// <param name="req"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpGet("detail_info")]
    public async Task<QueryAppDetailInfoCommandResponse> QueryAppDetailInfo([FromQuery] QueryAppDetailInfoCommand req, CancellationToken ct = default)
    {
        await CheckCanAccessAppAsync(req.AppId, ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 创建应用对话.
    /// </summary>
    /// <param name="req">创建对话请求，包含应用ID和话题标题.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="CreateAppChatCommandResponse"/>，包含新建对话的 ChatId.</returns>
    [HttpPost("create_chat")]
    public async Task<CreateAppChatCommandResponse> CreateChat([FromBody] CreateAppChatCommand req, CancellationToken ct = default)
    {
        await CheckCanAccessAppAsync(req.AppId, ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 删除对话.
    /// </summary>
    /// <param name="req"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpDelete("delete_chat")]
    public async Task<EmptyCommandResponse> DeleteChat([FromBody] DeleteAppChatCommand req, CancellationToken ct = default)
    {
        await CheckCanAccessAppAsync(req.AppId, ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 获取话题详细内容，即对话历史记录
    /// </summary>
    /// <param name="req">包含 ChatId 的查询命令对象（来自查询字符串）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>返回 <see cref="QueryAppChatHistoryCommandResponse"/>，包含话题历史记录明细</returns>
    [HttpGet("chat_history")]
    public async Task<QueryAppChatHistoryCommandResponse> QueryChatHistory([FromQuery] QueryAppChatHistoryCommand req, CancellationToken ct = default)
    {
        await CheckCanAccessAppAsync(req.AppId, ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 获取用户所有话题记录
    /// </summary>
    /// <param name="req"></param>
    /// <param name="ct">取消令牌</param>
    /// <returns>返回 <see cref="QueryAppChatTopicListCommandResponse"/>，包含用户所有话题列表</returns>
    [HttpPost("topic_list")]
    public async Task<QueryAppChatTopicListCommandResponse> QueryTopicList(QueryAppChatTopicListCommand req, CancellationToken ct = default)
    {
        await CheckCanAccessAppAsync(req.AppId, ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 修改对话标题.
    /// </summary>
    /// <param name="req"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpPost("update_chat_title")]
    public async Task<EmptyCommandResponse> UpdateChatTitle([FromBody] UpdateAppChatTitleCommand req, CancellationToken ct = default)
    {
        await CheckCanAccessAppAsync(req.AppId, ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 调试应用对话（不存储到数据库，用于配置验证）.
    /// </summary>
    /// <param name="command">调试对话命令，包含模型、提示词、插件、知识库等配置.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>流式返回 AI 对话响应.</returns>
    [HttpPost("debug_completions")]
    [Produces("text/event-stream")]
    [ProducesDefaultResponseType(typeof(AiProcessingChatItem))]
    public async Task Debug([FromBody] DebugAppChatCommand command, CancellationToken cancellationToken = default)
    {
        var existInTeam = await _mediator.Send(
            new QueryUserTeamRoleCommand
            {
                TeamId = command.TeamId,
                ContextUserId = _userContext.UserId,
            },
            cancellationToken);

        if (existInTeam.Role < TeamRole.Admin)
        {
            await WriteErrorAsync("没有操作权限", cancellationToken);
            return;
        }

        Response.ContentType = "text/event-stream; charset=utf-8";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["Connection"] = "keep-alive";
        Response.Headers["X-Accel-Buffering"] = "no";

        command.SetUserId(_userContext.UserId);

        try
        {
            await foreach (var item in _mediator.CreateStream(command, cancellationToken))
            {
                await WriteSseDataAsync(item, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Debug chat cancelled. TeamId: {TeamId}", command.TeamId);
            await WriteErrorAsync("请求已被中止", cancellationToken);
        }
        catch (BusinessException ex)
        {
            _logger.LogWarning(ex, "Business failure in debug chat. TeamId: {TeamId}", command.TeamId);
            await WriteErrorAsync(ex.Message, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected debug chat failure. TeamId: {TeamId}", command.TeamId);
            await WriteErrorAsync("调试对话处理失败，请稍后再试。", cancellationToken);
        }
    }

    /// <summary>
    /// 使用应用进行对话.
    /// </summary>
    /// <param name="command"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpPost("completions")]
    [Produces("text/event-stream")]
    [ProducesDefaultResponseType(typeof(AiProcessingChatItem))]
    public async Task Completions([FromBody] ProcessingAppChatCommand command, CancellationToken cancellationToken = default)
    {
        await CheckCanAccessAppAsync(command.AppId, cancellationToken);

        Response.ContentType = "text/event-stream; charset=utf-8";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["Connection"] = "keep-alive";
        Response.Headers["X-Accel-Buffering"] = "no";

        command.SetUserId(_userContext.UserId);

        try
        {
            await foreach (var item in _mediator.CreateStream(command, cancellationToken))
            {
                await WriteSseDataAsync(item, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Completions cancelled. AppId: {AppId}", command.AppId);
            await WriteErrorAsync("请求已被中止", cancellationToken);
        }
        catch (BusinessException ex)
        {
            _logger.LogWarning(ex, "Business failure in completions. AppId: {AppId}", command.AppId);
            await WriteErrorAsync(ex.Message, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected completions failure. AppId: {AppId}", command.AppId);
            await WriteErrorAsync("对话处理失败，请稍后再试。", cancellationToken);
        }
    }

    private async Task WriteSseDataAsync(object payload, CancellationToken cancellationToken)
    {
        if (HttpContext.RequestAborted.IsCancellationRequested)
        {
            return;
        }

        await HttpContext.Response
            .WriteAsync($"data: {payload.ToJsonString()}\n\n", cancellationToken);

        await Response.Body.FlushAsync(cancellationToken);
    }

    private Task WriteErrorAsync(string message, CancellationToken cancellationToken)
    {
        var errorPayload = new AiProcessingChatItem
        {
            FinishReason = "error",
            Choices = new[]
            {
                new AiProcessingChoice
                {
                    StreamType = AiProcessingChatStreamType.Text,
                    StreamState = AiProcessingChatStreamState.Error,
                    TextCall = new DefaultAiProcessingTextCall
                    {
                        Content = message
                    }
                }
            }
        };

        return WriteSseDataAsync(errorPayload, cancellationToken);
    }

    /// <summary>
    /// 检查用户是否可以访问应用（用户在团队内或应用是公开的）.
    /// </summary>
    private async Task CheckCanAccessAppAsync(Guid appId, CancellationToken ct)
    {
        var app = await _mediator.Send(
            new CheckAppAccessCommand
            {
                AppId = appId,
                ContextUserId = _userContext.UserId,
                ContextUserType = _userContext.UserType,
            },
            ct);

        if (!app.HasAccess)
        {
            throw new BusinessException("无权访问此应用") { StatusCode = 403 };
        }
    }
}

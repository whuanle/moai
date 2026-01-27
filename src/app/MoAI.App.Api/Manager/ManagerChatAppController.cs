using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using MoAI.AI.Models;
using MoAI.App.Chat.Manager.Commands;
using MoAI.App.Chat.Manager.Models;
using MoAI.App.Chat.Manager.Queries;
using MoAI.App.Chat.Works.Commands;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using MoAI.Team.Models;
using MoAI.Team.Queries;

namespace MoAI.App.Manager;

/// <summary>
/// 管理对话应用.
/// </summary>
[ApiController]
[Route("/app/team/chatapp")]
[EndpointGroupName("manage_chatapp")]
public class ManagerChatAppController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;
    private readonly ILogger<ManagerChatAppController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ManagerChatAppController"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    /// <param name="logger"></param>
    public ManagerChatAppController(IMediator mediator, UserContext userContext, ILogger<ManagerChatAppController> logger)
    {
        _mediator = mediator;
        _userContext = userContext;
        _logger = logger;
    }

    /// <summary>
    /// 获取应用配置.
    /// </summary>
    /// <param name="req"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpPost("config")]
    public async Task<QueryChatAppConfigCommandResponse> QueryConfig([FromBody] QueryChatAppConfigCommand req, CancellationToken ct = default)
    {
        await CheckIsAdminAsync(req.TeamId, ct);
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 修改应用配置.
    /// </summary>
    /// <param name="req"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpPost("update")]
    public async Task<EmptyCommandResponse> UpdateApp([FromBody] UpdateChatAppCommand req, CancellationToken ct = default)
    {
        await CheckIsAdminAsync(req.TeamId, ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 获取调试对话的历史记录.
    /// </summary>
    /// <param name="req"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpPost("query_debug_history")]
    public async Task<QueryDebugChatAppInstanceCommandResponse> QueryDebugChatId([FromBody] QueryDebugChatAppInstanceCommand req, CancellationToken ct = default)
    {
        await CheckIsAdminAsync(req.TeamId, ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 清空调试对话记录.
    /// </summary>
    /// <param name="req"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpPost("clear_debug_history")]
    public async Task<EmptyCommandResponse> QueryDebugChatId([FromBody] ClearDebugChatAppInstanceCommand req, CancellationToken ct = default)
    {
        await CheckIsAdminAsync(req.TeamId, ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 调试应用对话（不存储到数据库，用于配置验证）.
    /// </summary>
    /// <param name="req">调试对话命令，包含模型、提示词、插件、知识库等配置.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>流式返回 AI 对话响应.</returns>
    [HttpPost("debug_completions")]
    [Produces("text/event-stream")]
    [ProducesDefaultResponseType(typeof(AiProcessingChatItem))]
    public async Task Debug([FromBody] DebugChatAppInstanceCommand req, CancellationToken cancellationToken = default)
    {
        var existInTeam = await _mediator.Send(
            new QueryUserTeamRoleCommand
            {
                TeamId = req.TeamId,
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

        req.SetUserId(_userContext.UserId);

        try
        {
            await foreach (var item in _mediator.CreateStream(req, cancellationToken))
            {
                await WriteSseDataAsync(item, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Debug chat cancelled. TeamId: {TeamId}", req.TeamId);
            await WriteErrorAsync("请求已被中止", cancellationToken);
        }
        catch (BusinessException ex)
        {
            _logger.LogWarning(ex, "Business failure in debug chat. TeamId: {TeamId}", req.TeamId);
            await WriteErrorAsync(ex.Message, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected debug chat failure. TeamId: {TeamId}", req.TeamId);
            await WriteErrorAsync("调试对话处理失败，请稍后再试。", cancellationToken);
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

    private async Task CheckIsAdminAsync(int teamId, CancellationToken ct)
    {
        var existInTeam = await _mediator.Send(
            new QueryUserTeamRoleCommand
            {
                TeamId = teamId,
                ContextUserId = _userContext.UserId,
            },
            ct);

        if (existInTeam.Role < TeamRole.Admin)
        {
            throw new BusinessException("没有操作权限");
        }
    }
}

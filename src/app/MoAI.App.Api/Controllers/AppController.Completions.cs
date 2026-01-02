#pragma warning disable CA1031 // 不捕获常规异常类型

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MoAI.AI.Models;
using MoAI.App.Commands.Chat;
using MoAI.App.Queries.Chat;
using MoAI.App.Queries.Chat.Responses;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using MoAI.Team.Models;
using MoAI.Team.Queries;

namespace MoAI.App.Controllers;

/// <summary>
/// 应用对话调试.
/// </summary>
public partial class AppController : ControllerBase
{
    /// <summary>
    /// 删除对话.
    /// </summary>
    /// <param name="req"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpDelete("delete_chat")]
    public async Task<EmptyCommandResponse> DeleteChat([FromBody] DeleteAppChatCommand req, CancellationToken ct = default)
    {
        var existInTeam = await _mediator.Send(
            new QueryUserTeamRoleCommand
            {
                TeamId = req.TeamId,
                ContextUserId = _userContext.UserId,
            },
            ct);

        if (existInTeam.Role < TeamRole.Collaborator)
        {
            throw new BusinessException("未加入团队");
        }

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
        return await _mediator.Send(req, ct);
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
        var existInTeam = await _mediator.Send(
            new QueryUserTeamRoleCommand
            {
                TeamId = command.TeamId,
                ContextUserId = _userContext.UserId,
            },
            cancellationToken);

        if (existInTeam.Role < TeamRole.Admin)
        {
            await WriteErrorAsync("未加入团队", cancellationToken);
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
}

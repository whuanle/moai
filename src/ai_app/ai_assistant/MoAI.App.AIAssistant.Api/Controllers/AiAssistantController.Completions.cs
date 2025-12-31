#pragma warning disable CA1031 // 不捕获常规异常类型

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MoAI.AI.Models;
using MoAI.App.AIAssistant.Commands;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;

namespace MoAI.App.AIAssistant.Controllers;

/// <summary>
/// 对话生成.
/// </summary>
public partial class AiAssistantController : ControllerBase
{
    /// <summary>
    /// 进行流式对话.
    /// </summary>
    /// <param name="command">对话参数.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpPost("completions")]
    [Produces("text/event-stream")]
    [ProducesDefaultResponseType(typeof(AiProcessingChatItem))]
    public async Task Completions([FromBody] ProcessingAiAssistantChatCommand command, CancellationToken cancellationToken = default)
    {
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
            _logger.LogInformation("Completions cancelled. ChatId: {ChatId}", command.ChatId);
            await WriteErrorAsync("请求已被中止", cancellationToken);
        }
        catch (BusinessException ex)
        {
            _logger.LogWarning(ex, "Business failure in completions. ChatId: {ChatId}", command.ChatId);
            await WriteErrorAsync(ex.Message, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected completions failure. ChatId: {ChatId}", command.ChatId);
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
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MoAI.AI.Models;
using MoAI.App.AIAssistant.Commands;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using System.Text;

namespace MoAI.App.AIAssistant.Controllers;

/// <summary>
/// 聊天.
/// </summary>
public partial class AiAssistantController : ControllerBase
{
    /// <summary>
    /// 对话.
    /// </summary>
    /// <param name="command">对话参数.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpPost("completions")]
    public async Task Completions([FromBody] ProcessingAiAssistantChatCommand command, CancellationToken cancellationToken)
    {
        Response.ContentType = "text/event-stream";

        var req = new ProcessingAiAssistantChatCommand
        {
            ChatId = command.ChatId,
            Content = command.Content,
            UserId = _userContext.UserId,
        };

        await foreach (var item in _mediator.CreateStream(req, cancellationToken))
        {
            if (item is OpenAIChatCompletionsChunk chunk)
            {
                await HttpContext.Response.WriteAsync(
                    "data: " + chunk.ToJsonString() + "\n\n", Encoding.UTF8);
            }
            else if (item is OpenAIChatCompletionsObject chatObject)
            {
                await HttpContext.Response.WriteAsync(
                    "data: " + chatObject.ToJsonString() + "\n\n", Encoding.UTF8);
            }
            else
            {
            }

            await Response.Body.FlushAsync();
        }
    }
}

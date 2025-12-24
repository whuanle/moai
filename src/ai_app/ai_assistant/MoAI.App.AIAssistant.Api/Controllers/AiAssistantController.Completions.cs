using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.KernelMemory.DataFormats;
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
        Response.ContentType = "text/event-stream";

        command.SetUserId(_userContext.UserId);

        await foreach (var item in _mediator.CreateStream(command, cancellationToken))
        {
            await HttpContext.Response.WriteAsync(
                "data: " + item.ToJsonString() + "\n\n", Encoding.UTF8);

            await Response.Body.FlushAsync();
        }
    }
}

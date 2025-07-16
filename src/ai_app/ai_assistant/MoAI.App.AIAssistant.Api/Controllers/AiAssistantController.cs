// <copyright file="AiAssistantController.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MoAI.AI.Models;
using MoAI.App.AIAssistant.Commands;
using MoAI.Infra.Extensions;
using System.Text;

namespace MoAI.App.AIAssistant.Controllers;

/// <summary>
/// 聊天.
/// </summary>
[Route("/api/app/assistant")]
public class AiAssistantController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiAssistantController"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    public AiAssistantController(IMediator mediator)
    {
        _mediator = mediator;
    }

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

        await foreach (var item in _mediator.CreateStream(request: command, cancellationToken))
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

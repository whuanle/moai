using MaomiAI.AI.Models;
using MaomiAI.AiModel.Shared.Models;
using MediatR;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MaomiAI.Chat.Shared.Commands.Responses;

public class CreateAiAssistantChatCommandResponse
{
    /// <summary>
    /// 每个聊天对话都有唯一 id.
    /// </summary>
    public string ChatId { get; init; }
}
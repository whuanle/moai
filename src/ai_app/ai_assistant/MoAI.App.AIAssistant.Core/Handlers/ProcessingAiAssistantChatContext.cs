#pragma warning disable SKEXP0001 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。
#pragma warning disable SKEXP0040 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。
#pragma warning disable CA1849 // 当在异步方法中时，调用异步方法

using Maomi.MQ;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using MoAI.AI.ChatCompletion;
using MoAI.AI.Models;
using MoAI.AiModel.Models;
using MoAI.App.AIAssistant.Commands;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using MoAI.Plugin;
using StackExchange.Redis.Extensions.Core.Abstractions;
using System.Runtime.CompilerServices;
using System.Text;

namespace MoAI.App.AIAssistant.Handlers;

/// <summary>
/// 对话上下文.
/// </summary>
public class ProcessingAiAssistantChatContext
{
    /// <summary>
    /// 对话id.
    /// </summary>
    public Guid ChatId { get; init; }

    /// <summary>
    /// 使用的对话模型.
    /// </summary>
    public AiEndpoint AiModel { get; init; } = default!;

    /// <summary>
    /// 对话记录.
    /// </summary>
    public List<DefaultAiProcessingChoice> Choices { get; init; } = new();

    /// <summary>
    /// 插件键名映射.
    /// </summary>
    public Dictionary<string, string> PluginKeyNames { get; init; } = new();
}

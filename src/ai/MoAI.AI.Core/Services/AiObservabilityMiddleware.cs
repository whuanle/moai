using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using MoAI.Infra;

namespace MoAI.AI.Services;

/// <summary>
/// AI 应用 OpenTelemetry 调试埋点（系统设置 MoAI:Debug=true 时由构建方挂接）：
/// Agent 层与 Chat 层均挂 <c>UseOpenTelemetry</c> 且开启 <c>EnableSensitiveData</c>，
/// Span 携带消息正文、系统提示词、工具定义与入参出参；事件源名须与 ConfigureOpenTelemetryModule 的 AddSource 配对.
/// </summary>
public static class AiObservabilityMiddleware
{
    /// <summary>
    /// 模型调用埋点：每次模型补全产生 Span 与 gen_ai 指标，应挂在最贴近协议 SDK 的内层.
    /// </summary>
    /// <param name="chatClient">内层对话客户端.</param>
    /// <returns>包装后的对话客户端.</returns>
    public static IChatClient WrapChatClient(IChatClient chatClient)
    {
        return chatClient.AsBuilder()
            .UseOpenTelemetry(sourceName: AppConst.AiTraceSourceName, configure: cfg => cfg.EnableSensitiveData = true)
            .Build();
    }

    /// <summary>
    /// Agent 调用埋点：<c>invoke_agent</c> Span 携带对话正文与工具调用明细；
    /// 应包在脱敏中间件之外，保证 Span 只记录脱敏后内容.
    /// </summary>
    /// <param name="agent">内层 Agent.</param>
    /// <returns>包装后的 Agent.</returns>
    public static AIAgent WrapAgent(AIAgent agent)
    {
        return agent.AsBuilder()
            .UseOpenTelemetry(sourceName: AppConst.AiTraceSourceName, configure: cfg => cfg.EnableSensitiveData = true)
            .Build();
    }
}

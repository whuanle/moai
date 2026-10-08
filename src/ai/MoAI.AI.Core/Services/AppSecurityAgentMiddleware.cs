using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using MoAI.Database.Aggregates;

namespace MoAI.AI.Services;

/// <summary>
/// 应用安全脱敏中间件（Microsoft Agent Framework 官方推荐的 <c>AIAgentBuilder.Use</c> 装饰器拦截）：
/// <para>1. 函数调用中间件：工具调用返回后对结果脱敏——脱敏发生在 <c>FunctionResultContent</c> 生成源头，
/// 模型可见内容、AG-UI 工具结果事件与会话历史同时生效（对应 MAF 示例 Agent_Step11_Middleware 的结果改写）；</para>
/// <para>2. Run 级中间件：对输出中的模型正文与工具调用参数脱敏（对应 MAF 官方 PIIMiddleware 模式）；
/// 流式路径产出新 update 实例而非就地修改，避免污染 FunctionInvokingChatClient 尚未执行的原始调用参数.</para>
/// </summary>
public static class AppSecurityAgentMiddleware
{
    /// <summary>
    /// 为 Agent 按策略挂接脱敏中间件；策略未生效时原样返回.
    /// </summary>
    /// <param name="agent">内层 Agent.</param>
    /// <param name="policy">脱敏策略.</param>
    /// <returns>包装后的 Agent.</returns>
    public static AIAgent Wrap(AIAgent agent, AppSecurityPolicy policy)
    {
        if (!policy.IsActive)
        {
            return agent;
        }

        if (policy.ToolResultEnabled)
        {
            agent = agent.AsBuilder()
                .Use((_, context, next, cancellationToken) => MaskFunctionResultAsync(policy, context, next, cancellationToken))
                .Build();
        }

        if (policy.ToolArgsEnabled || policy.ModelOutputEnabled)
        {
            agent = agent.AsBuilder()
                .Use(
                    (messages, session, options, innerAgent, cancellationToken) => RunAsync(policy, messages, session, options, innerAgent, cancellationToken),
                    (messages, session, options, innerAgent, cancellationToken) => RunStreamingAsync(policy, messages, session, options, innerAgent, cancellationToken))
                .Build();
        }

        return agent;
    }

    /// <summary>
    /// 函数调用中间件：next 之后改写工具返回值.
    /// MEAI 会把工具返回值编组为 <see cref="System.Text.Json.JsonElement"/>（字符串/对象形态均可能出现），
    /// 此处统一归一为文本做 JSON 感知脱敏，命中后以脱敏文本返回.
    /// </summary>
    private static async ValueTask<object?> MaskFunctionResultAsync(
        AppSecurityPolicy policy,
        FunctionInvocationContext context,
        Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
        CancellationToken cancellationToken)
    {
        var result = await next(context, cancellationToken).ConfigureAwait(false);
        if (result is null)
        {
            return null;
        }

        var raw = result switch
        {
            string text => text,
            System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.String } je => je.GetString(),
            System.Text.Json.JsonElement je => je.GetRawText(),
            _ => JsonSerializer.Serialize(result),
        };
        if (raw is null)
        {
            return result;
        }

        var masked = policy.MaskToolResultText(raw);
        return masked == raw ? result : masked;
    }

    private static async Task<AgentResponse> RunAsync(
        AppSecurityPolicy policy,
        IEnumerable<ChatMessage> messages,
        AgentSession? session,
        AgentRunOptions? options,
        AIAgent innerAgent,
        CancellationToken cancellationToken)
    {
        var response = await innerAgent.RunAsync(messages, session, options, cancellationToken).ConfigureAwait(false);

        // 非流式路径：运行已结束，就地改写响应消息内容（官方 PIIMiddleware 模式）
        if (response.Messages is { Count: > 0 })
        {
            foreach (var message in response.Messages)
            {
                MaskMessageContents(policy, message.Contents);
            }
        }

        return response;
    }

    private static async IAsyncEnumerable<AgentResponseUpdate> RunStreamingAsync(
        AppSecurityPolicy policy,
        IEnumerable<ChatMessage> messages,
        AgentSession? session,
        AgentRunOptions? options,
        AIAgent innerAgent,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var update in innerAgent.RunStreamingAsync(messages, session, options, cancellationToken).ConfigureAwait(false))
        {
            yield return MaskUpdate(policy, update);
        }
    }

    private static void MaskMessageContents(AppSecurityPolicy policy, IList<AIContent> contents)
    {
        foreach (var content in contents)
        {
            switch (content)
            {
                case TextContent text when policy.ModelOutputEnabled:
                    text.Text = policy.MaskModelText(text.Text);
                    break;
                case FunctionCallContent call when policy.ToolArgsEnabled:
                    call.Arguments = MaskArguments(policy, call.Arguments);
                    break;
            }
        }
    }

    /// <summary>
    /// 流式路径：产出新 update 实例（内容命中脱敏时），内层原件保持原样.
    /// </summary>
    private static AgentResponseUpdate MaskUpdate(AppSecurityPolicy policy, AgentResponseUpdate update)
    {
        if (update.Contents is not { Count: > 0 } contents)
        {
            return update;
        }

        var changed = false;
        var newContents = new List<AIContent>(contents.Count);
        foreach (var content in contents)
        {
            switch (content)
            {
                case TextContent text when policy.ModelOutputEnabled:
                {
                    var masked = policy.MaskModelText(text.Text);
                    if (masked != text.Text)
                    {
                        changed = true;
                        newContents.Add(new TextContent(masked) { AdditionalProperties = text.AdditionalProperties });
                    }
                    else
                    {
                        newContents.Add(content);
                    }

                    break;
                }

                case FunctionCallContent call when policy.ToolArgsEnabled:
                {
                    var masked = MaskArguments(policy, call.Arguments);
                    changed = true;
                    newContents.Add(new FunctionCallContent(call.CallId, call.Name) { Arguments = masked });
                    break;
                }

                default:
                    newContents.Add(content);
                    break;
            }
        }

        if (!changed)
        {
            return update;
        }

        return new AgentResponseUpdate(update.Role, newContents)
        {
            AuthorName = update.AuthorName,
            AgentId = update.AgentId,
            ResponseId = update.ResponseId,
            MessageId = update.MessageId,
            CreatedAt = update.CreatedAt,
            RawRepresentation = update.RawRepresentation,
            AdditionalProperties = update.AdditionalProperties,
            ContinuationToken = update.ContinuationToken,
            FinishReason = update.FinishReason,
        };
    }

    /// <summary>
    /// 工具调用参数脱敏：字符串值按规则脱敏，嵌套字典递归，其余类型原样保留.
    /// </summary>
    private static IDictionary<string, object?>? MaskArguments(AppSecurityPolicy policy, IDictionary<string, object?>? arguments)
    {
        if (arguments == null || arguments.Count == 0)
        {
            return arguments;
        }

        var masked = new Dictionary<string, object?>(arguments.Count);
        foreach (var (key, value) in arguments)
        {
            masked[key] = value switch
            {
                string text => policy.MaskToolArgValue(text),
                IDictionary<string, object?> nested => MaskArguments(policy, nested),
                _ => value,
            };
        }

        return masked;
    }
}

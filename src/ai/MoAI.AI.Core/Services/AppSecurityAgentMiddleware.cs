using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using MoAI.Database.Aggregates;
using MoAI.Infra.Exceptions;

namespace MoAI.AI.Services;

/// <summary>
/// 应用安全中间件（Microsoft Agent Framework 官方推荐的 <c>AIAgentBuilder.Use</c> 装饰器拦截）：
/// <para>1. 函数调用中间件：执行前按 block 策略预检工具参数（命中则不执行、向模型返回拒绝应答），
/// 执行后对结果强制处理——脱敏/拦截发生在 <c>FunctionResultContent</c> 生成源头，
/// 模型可见内容、AG-UI 工具结果事件与会话历史同时生效；</para>
/// <para>2. Run 级中间件：用户输入检查点（block 命中拒绝本轮对话）、输出中的模型正文与工具调用参数脱敏；
/// 流式路径产出新 update 实例而非就地修改，避免污染 FunctionInvokingChatClient 尚未执行的原始调用参数.</para>
/// <para>3. 命中写入安全审计（AppSecurityAuditWriter → app_security_event），会话/用户归属经 AsyncLocal 从 Run 级传递到函数级.</para>
/// </summary>
public static class AppSecurityAgentMiddleware
{
    /// <summary>
    /// 用户输入被 block 策略拦截时的提示文本（作为本轮回复返回并落库）.
    /// </summary>
    public const string BlockedInputNotice = "[应用安全策略] 输入内容包含被禁止的信息，本条消息已被拦截。";

    /// <summary>
    /// 工具调用被 block 策略拦截时返回给模型的拒绝应答（不执行工具，模型可据此调整）.
    /// </summary>
    public const string BlockedToolCallNotice = "[应用安全策略] 该工具调用的参数包含被禁止的信息，调用已拦截。";

    /// <summary>
    /// 模型回复被 block 策略拦截时下发给用户的提示文本（缓冲窗截断后续输出）.
    /// </summary>
    public const string BlockedModelOutputNotice = "[应用安全策略] 回复内容包含被禁止的信息，输出已被拦截。";

    /// <summary>
    /// 流式缓冲窗：积压超过该字符数才做释放判定（泄漏窗口与首字延迟的折中）.
    /// </summary>
    private const int HoldBackChars = 120;

    /// <summary>
    /// 流式缓冲窗：每次释放保留的尾部字符数（与后续文本合并重检，覆盖跨 chunk 边界命中）.
    /// </summary>
    private const int TailKeepChars = 32;

    private static readonly AsyncLocal<RunIdentity?> RunIdentityCurrent = new();

    /// <summary>
    /// 为 Agent 按策略挂接安全中间件；策略未生效时原样返回.
    /// </summary>
    /// <param name="agent">内层 Agent.</param>
    /// <param name="policy">安全策略.</param>
    /// <param name="auditWriter">安全事件写入器（可空，空时不写审计）.</param>
    /// <returns>包装后的 Agent.</returns>
    public static AIAgent Wrap(AIAgent agent, AppSecurityPolicy policy, AppSecurityAuditWriter? auditWriter = null)
    {
        if (!policy.IsActive)
        {
            return agent;
        }

        if (policy.ToolResultEnabled || policy.ToolArgsEnabled)
        {
            agent = agent.AsBuilder()
                .Use((_, context, next, cancellationToken) => FunctionMiddlewareAsync(policy, auditWriter, context, next, cancellationToken))
                .Build();
        }

        if (policy.ToolArgsEnabled || policy.ModelOutputEnabled || policy.InputEnabled)
        {
            agent = agent.AsBuilder()
                .Use(
                    (messages, session, options, innerAgent, cancellationToken) => RunAsync(policy, auditWriter, messages, session, options, innerAgent, cancellationToken),
                    (messages, session, options, innerAgent, cancellationToken) => RunStreamingAsync(policy, auditWriter, messages, session, options, innerAgent, cancellationToken))
                .Build();
        }

        return agent;
    }

    /// <summary>
    /// 函数调用中间件：执行前 block 预检（命中拒绝执行），next 之后强制处理工具返回值.
    /// MEAI 会把工具返回值编组为 <see cref="System.Text.Json.JsonElement"/>（字符串/对象形态均可能出现），
    /// 此处统一归一为文本做 JSON 感知处理，命中后以处理后文本返回.
    /// </summary>
    private static async ValueTask<object?> FunctionMiddlewareAsync(
        AppSecurityPolicy policy,
        AppSecurityAuditWriter? auditWriter,
        FunctionInvocationContext context,
        Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
        CancellationToken cancellationToken)
    {
        var identity = RunIdentityCurrent.Value;
        var collector = new AppSecurityHitCollector();

        // 执行前拦截：参数 block 命中则不执行工具，模型收到拒绝应答可自行调整
        if (policy.ToolArgsEnabled && policy.IsToolCallBlocked(context.Arguments, collector))
        {
            FlushAudit(auditWriter, policy, identity, collector);
            return BlockedToolCallNotice;
        }

        var result = await next(context, cancellationToken).ConfigureAwait(false);
        if (result is null)
        {
            FlushAudit(auditWriter, policy, identity, collector);
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
            FlushAudit(auditWriter, policy, identity, collector);
            return result;
        }

        var outcome = policy.EnforceToolResultText(raw, collector);
        FlushAudit(auditWriter, policy, identity, collector);
        return outcome.Text == raw ? result : outcome.Text;
    }

    private static async Task<AgentResponse> RunAsync(
        AppSecurityPolicy policy,
        AppSecurityAuditWriter? auditWriter,
        IEnumerable<ChatMessage> messages,
        AgentSession? session,
        AgentRunOptions? options,
        AIAgent innerAgent,
        CancellationToken cancellationToken)
    {
        var collector = new AppSecurityHitCollector();
        var requestMessages = ProcessInputMessages(policy, collector, messages, out var inputBlocked);
        if (inputBlocked)
        {
            FlushAudit(auditWriter, policy, session, collector);
            throw new BusinessException(BlockedInputNotice) { StatusCode = 400 };
        }

        RunIdentityCurrent.Value = RunIdentity.From(session);
        try
        {
            var response = await innerAgent.RunAsync(requestMessages, session, options, cancellationToken).ConfigureAwait(false);

            // 非流式路径：运行已结束，就地改写响应消息内容（官方 PIIMiddleware 模式）
            if (response.Messages is { Count: > 0 })
            {
                foreach (var message in response.Messages)
                {
                    MaskMessageContents(policy, collector, message.Contents);
                }
            }

            return response;
        }
        finally
        {
            RunIdentityCurrent.Value = null;
            FlushAudit(auditWriter, policy, session, collector);
        }
    }

    private static async IAsyncEnumerable<AgentResponseUpdate> RunStreamingAsync(
        AppSecurityPolicy policy,
        AppSecurityAuditWriter? auditWriter,
        IEnumerable<ChatMessage> messages,
        AgentSession? session,
        AgentRunOptions? options,
        AIAgent innerAgent,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var collector = new AppSecurityHitCollector();
        var requestMessages = ProcessInputMessages(policy, collector, messages, out var inputBlocked);
        if (inputBlocked)
        {
            FlushAudit(auditWriter, policy, session, collector);
            yield return new AgentResponseUpdate(ChatRole.Assistant, BlockedInputNotice);
            yield break;
        }

        RunIdentityCurrent.Value = RunIdentity.From(session);
        try
        {
            if (policy.ModelBlockActive)
            {
                // 模型回复含 block 规则：缓冲窗路径——文本先积压，释放前整段检测，命中截断输出
                var buffer = new ModelOutputBuffer(policy, collector);
                await foreach (var update in innerAgent.RunStreamingAsync(requestMessages, session, options, cancellationToken).ConfigureAwait(false))
                {
                    foreach (var emit in buffer.Accept(update))
                    {
                        yield return emit;
                    }

                    if (buffer.Blocked)
                    {
                        yield return new AgentResponseUpdate(ChatRole.Assistant, BlockedModelOutputNotice);
                        yield break;
                    }
                }

                foreach (var emit in buffer.FlushRemainder())
                {
                    yield return emit;
                }

                if (buffer.Blocked)
                {
                    yield return new AgentResponseUpdate(ChatRole.Assistant, BlockedModelOutputNotice);
                }

                yield break;
            }

            await foreach (var update in innerAgent.RunStreamingAsync(requestMessages, session, options, cancellationToken).ConfigureAwait(false))
            {
                yield return MaskUpdate(policy, collector, update);
            }
        }
        finally
        {
            RunIdentityCurrent.Value = null;
            FlushAudit(auditWriter, policy, session, collector);
        }
    }

    /// <summary>
    /// 用户输入检查点：对 user 消息文本按「用户输入」范围强制处理（block 命中 → 拒绝本轮）.
    /// 返回新消息列表（未命中且无修改时原列表返回），不改动调用方原消息.
    /// </summary>
    private static IReadOnlyList<ChatMessage> ProcessInputMessages(AppSecurityPolicy policy, AppSecurityHitCollector collector, IEnumerable<ChatMessage> messages, out bool blocked)
    {
        blocked = false;
        if (!policy.InputEnabled)
        {
            return messages as IReadOnlyList<ChatMessage> ?? [.. messages];
        }

        var source = messages as IReadOnlyList<ChatMessage> ?? [.. messages];
        List<ChatMessage>? result = null;
        for (var i = 0; i < source.Count; i++)
        {
            var message = source[i];
            if (message.Role != ChatRole.User)
            {
                continue;
            }

            List<AIContent>? newContents = null;
            foreach (var content in message.Contents)
            {
                if (content is not TextContent textContent || string.IsNullOrEmpty(textContent.Text))
                {
                    continue;
                }

                var outcome = policy.EnforceInputText(textContent.Text, collector);
                if (outcome.Blocked)
                {
                    blocked = true;
                }

                if (outcome.Text != textContent.Text)
                {
                    newContents ??= [.. message.Contents];
                    newContents[newContents.IndexOf(content)] = new TextContent(outcome.Text) { AdditionalProperties = textContent.AdditionalProperties };
                }
            }

            if (newContents != null)
            {
                result ??= [.. source];
                result[i] = new ChatMessage(message.Role, newContents)
                {
                    AuthorName = message.AuthorName,
                    CreatedAt = message.CreatedAt,
                    RawRepresentation = message.RawRepresentation,
                    AdditionalProperties = message.AdditionalProperties,
                };
            }
        }

        return result ?? source;
    }

    private static void MaskMessageContents(AppSecurityPolicy policy, AppSecurityHitCollector collector, IList<AIContent> contents)
    {
        foreach (var content in contents)
        {
            switch (content)
            {
                case TextContent text when policy.ModelOutputEnabled:
                {
                    // 非流式整条路径：先按非 block 策略脱敏，再对结果做 block 检测，命中整段替换
                    var outcome = policy.EnforceModelText(text.Text, collector);
                    text.Text = outcome.Text;
                    break;
                }
                case FunctionCallContent call when policy.ToolArgsEnabled:
                    call.Arguments = MaskArguments(policy, collector, call.Arguments);
                    break;
            }
        }
    }

    /// <summary>
    /// 流式路径：产出新 update 实例（内容命中处理时），内层原件保持原样.
    /// </summary>
    private static AgentResponseUpdate MaskUpdate(AppSecurityPolicy policy, AppSecurityHitCollector collector, AgentResponseUpdate update)
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
                    var masked = policy.MaskModelText(text.Text, collector);
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
                    var masked = MaskArguments(policy, collector, call.Arguments);
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

        // 命中改写时丢弃 RawRepresentation：AGUI 适配器优先从原始 SDK chunk 读取文本，
        // 保留会让已脱敏的 Contents 被原文绕过
        return new AgentResponseUpdate(update.Role, newContents)
        {
            AuthorName = update.AuthorName,
            AgentId = update.AgentId,
            ResponseId = update.ResponseId,
            MessageId = update.MessageId,
            CreatedAt = update.CreatedAt,
            AdditionalProperties = update.AdditionalProperties,
            ContinuationToken = update.ContinuationToken,
            FinishReason = update.FinishReason,
        };
    }

    /// <summary>
    /// 工具调用参数脱敏：字符串值按规则处理，嵌套字典递归，其余类型原样保留.
    /// </summary>
    private static IDictionary<string, object?>? MaskArguments(AppSecurityPolicy policy, AppSecurityHitCollector collector, IDictionary<string, object?>? arguments)
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
                string text => policy.MaskToolArgValue(text, collector),
                IDictionary<string, object?> nested => MaskArguments(policy, collector, nested),
                _ => value,
            };
        }

        return masked;
    }

    private static void FlushAudit(AppSecurityAuditWriter? auditWriter, AppSecurityPolicy policy, AgentSession? session, AppSecurityHitCollector collector)
    {
        FlushAudit(auditWriter, policy, RunIdentity.From(session), collector);
    }

    private static void FlushAudit(AppSecurityAuditWriter? auditWriter, AppSecurityPolicy policy, RunIdentity? identity, AppSecurityHitCollector collector)
    {
        if (auditWriter == null)
        {
            return;
        }

        var hits = collector.Snapshot();
        if (hits.Count == 0)
        {
            return;
        }

        collector.Reset();
        auditWriter.Write(policy.AppId, policy.TeamId, identity?.SessionId, identity?.UserId, hits);
    }

    /// <summary>
    /// 模型输出缓冲窗：文本积压到阈值后先整段做 block 检测再释放（保留尾部与后续合并重检，覆盖跨 chunk 边界）；
    /// 非 block 策略（redact/mask/hash）在入缓冲前逐 chunk 应用；命中即置 Blocked 并丢弃未释放内容。
    /// </summary>
    private sealed class ModelOutputBuffer
    {
        private readonly AppSecurityPolicy _policy;
        private readonly AppSecurityHitCollector _collector;
        private string _pending = string.Empty;

        public ModelOutputBuffer(AppSecurityPolicy policy, AppSecurityHitCollector collector)
        {
            _policy = policy;
            _collector = collector;
        }

        /// <summary>
        /// 是否已发生 block 拦截（置位后不再释放任何内容）.
        /// </summary>
        public bool Blocked { get; private set; }

        /// <summary>
        /// 接收一个流式 update：文本积压检测，非文本内容先清空缓冲再透传；返回应向下游产出的 update 序列.
        /// </summary>
        public IEnumerable<AgentResponseUpdate> Accept(AgentResponseUpdate update)
        {
            if (Blocked)
            {
                yield break;
            }

            if (update.Contents is not { Count: > 0 } contents)
            {
                yield return update;
                yield break;
            }

            foreach (var content in contents)
            {
                if (content is TextContent text && _policy.ModelOutputEnabled)
                {
                    _pending += _policy.MaskModelTextExcludeBlock(text.Text, _collector);
                    foreach (var emit in TryRelease(partial: true))
                    {
                        yield return emit;
                    }

                    if (Blocked)
                    {
                        yield break;
                    }

                    continue;
                }

                // 非文本内容（工具调用等）穿插时先清空缓冲（含检测），再透传原 update
                foreach (var emit in TryRelease(partial: false))
                {
                    yield return emit;
                }

                if (Blocked)
                {
                    yield break;
                }

                yield return MaskUpdate(_policy, _collector, update);
            }
        }

        /// <summary>
        /// 流结束：对剩余缓冲做最终检测，未命中全部释放.
        /// </summary>
        public IEnumerable<AgentResponseUpdate> FlushRemainder()
        {
            if (Blocked || _pending.Length == 0)
            {
                yield break;
            }

            if (_policy.IsModelTextBlocked(_pending, _collector))
            {
                Blocked = true;
                _pending = string.Empty;
                yield break;
            }

            var text = _pending;
            _pending = string.Empty;
            yield return TextUpdate(text);
        }

        private IEnumerable<AgentResponseUpdate> TryRelease(bool partial)
        {
            if (Blocked || _pending.Length == 0)
            {
                yield break;
            }

            if (_policy.IsModelTextBlocked(_pending, _collector))
            {
                Blocked = true;
                _pending = string.Empty;
                yield break;
            }

            var releaseLen = partial ? _pending.Length - TailKeepChars : _pending.Length;
            if (releaseLen <= 0)
            {
                yield break;
            }

            var text = _pending[..releaseLen];
            _pending = _pending[releaseLen..];
            yield return TextUpdate(text);
        }

        private static AgentResponseUpdate TextUpdate(string text)
        {
            return new AgentResponseUpdate(ChatRole.Assistant, [new TextContent(text)]);
        }
    }

    private sealed record RunIdentity(Guid? SessionId, long? UserId)
    {
        public static RunIdentity? From(AgentSession? session)
        {
            if (session == null)
            {
                return null;
            }

            Guid? sessionId = null;
            var sessionIdValue = session.StateBag.GetValue<string>(AppAgentConstants.SessionIdStateKey);
            if (Guid.TryParse(sessionIdValue, out var parsedSessionId))
            {
                sessionId = parsedSessionId;
            }

            var rawUserId = session.StateBag.GetValue<string>(AppAgentConstants.UserIdStateKey);
            long? userId = long.TryParse(rawUserId, out var parsedUserId) ? parsedUserId : null;
            return new RunIdentity(sessionId, userId);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Maomi;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using MoAI.AI.Services;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.AI.Acp;

/// <summary>
/// 应用 ACP（Agent Client Protocol）服务端：把应用 Agent（Agent 应用 / Workflow 应用）以 ACP 协议
/// 暴露给外部 agent 调用，会话与执行管线与 AG-UI 对话完全同源（<see cref="AppAgentFactory"/> 装配 +
/// 热态快照 + 落库压缩）。
/// <para>传输为 MoAI HTTP 承载（ACP v1 官方仅定义 stdio，服务端场景映射为 HTTP）：POST 单条 JSON-RPC 请求，
/// <c>session/prompt</c> 以 text/event-stream 逐条下发 <c>session/update</c> 通知并以最终 JSON-RPC
/// response（stopReason）收口；其余方法直接 application/json 返回。认证与门禁（app_acp 范围、团队归属、
/// 已发布应用、外部用户语义）由 ExternalAuthenticationMiddleware 前置完成.</para>
/// </summary>
[InjectOnScoped]
public sealed class AppAcpServer
{
    /// <summary>
    /// MoAI ACP 承载支持的协议版本（ACP v1）.
    /// </summary>
    private const int ProtocolVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly DatabaseContext _databaseContext;
    private readonly AppAgentFactory _agentFactory;
    private readonly AppChatHotStore _hotStore;
    private readonly AppChatFlushService _flushService;
    private readonly AppAcpRunRegistry _runRegistry;
    private readonly ILogger<AppAcpServer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppAcpServer"/> class.
    /// </summary>
    public AppAcpServer(
        DatabaseContext databaseContext,
        AppAgentFactory agentFactory,
        AppChatHotStore hotStore,
        AppChatFlushService flushService,
        AppAcpRunRegistry runRegistry,
        ILogger<AppAcpServer> logger)
    {
        _databaseContext = databaseContext;
        _agentFactory = agentFactory;
        _hotStore = hotStore;
        _flushService = flushService;
        _runRegistry = runRegistry;
        _logger = logger;
    }

    /// <summary>
    /// 处理一条 JSON-RPC 请求（认证与门禁已由中间件完成）.
    /// </summary>
    /// <param name="context">HTTP 上下文.</param>
    /// <param name="appId">路由应用 id.</param>
    /// <param name="cancellationToken">请求取消令牌.</param>
    public async Task HandleAsync(HttpContext context, Guid appId, CancellationToken cancellationToken)
    {
        JsonDocument document;
        try
        {
            document = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: cancellationToken);
        }
        catch (JsonException)
        {
            await WriteJsonAsync(context, JsonRpcError(null, -32700, "Parse error."));
            return;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                await WriteJsonAsync(context, JsonRpcError(null, -32600, "Invalid Request."));
                return;
            }

            if (root.TryGetProperty("jsonrpc", out var jsonRpc) && jsonRpc.ValueKind == JsonValueKind.String && jsonRpc.GetString() != "2.0")
            {
                await WriteJsonAsync(context, JsonRpcError(null, -32600, "Invalid Request: jsonrpc must be \"2.0\"."));
                return;
            }

            if (!root.TryGetProperty("method", out var methodNode) || methodNode.ValueKind != JsonValueKind.String)
            {
                await WriteJsonAsync(context, JsonRpcError(null, -32600, "Invalid Request: method is required."));
                return;
            }

            var method = methodNode.GetString()!;
            var hasId = root.TryGetProperty("id", out var idNode) && idNode.ValueKind != JsonValueKind.Null;
            var id = hasId ? idNode.Clone() : (JsonElement?)null;
            root.TryGetProperty("params", out var paramsNode);

            if (method == "session/prompt")
            {
                if (!hasId)
                {
                    // 通知形态的 prompt 无法回传流式结果，按不可处理返回
                    context.Response.StatusCode = StatusCodes.Status202Accepted;
                    return;
                }

                await HandlePromptAsync(context, appId, id.Value, paramsNode, cancellationToken);
                return;
            }

            if (!hasId)
            {
                // 通知：session/cancel 幂等取消进行中的一轮对话，其余通知忽略
                if (method == "session/cancel")
                {
                    TryCancel(paramsNode);
                }

                context.Response.StatusCode = StatusCodes.Status202Accepted;
                return;
            }

            switch (method)
            {
                case "initialize":
                    await WriteJsonAsync(context, JsonRpcResult(id.Value, BuildInitializeResult()));
                    return;

                case "session/new":
                    await HandleSessionNewAsync(context, appId, id.Value, cancellationToken);
                    return;

                case "session/load":
                    await HandleSessionLoadAsync(context, appId, id.Value, paramsNode, cancellationToken);
                    return;

                case "session/cancel":
                    TryCancel(paramsNode);
                    await WriteJsonAsync(context, JsonRpcResult(id.Value, new JsonObject()));
                    return;

                default:
                    await WriteJsonAsync(context, JsonRpcError(id.Value, -32601, $"Method not found: {method}."));
                    return;
            }
        }
    }

    /// <summary>
    /// initialize 握手：返回协议版本与 Agent 能力（支持会话恢复；不支持提示词命令与 MCP 资源）.
    /// </summary>
    private static JsonObject BuildInitializeResult()
    {
        return new JsonObject
        {
            ["protocolVersion"] = ProtocolVersion,
            ["agentCapabilities"] = new JsonObject
            {
                ["loadSession"] = true,
                ["prompts"] = new JsonObject { ["available"] = false },
                ["mcpCapabilities"] = new JsonObject { ["http"] = false, ["sse"] = false },
            },
            ["authMethods"] = new JsonArray(),
        };
    }

    /// <summary>
    /// session/new：为当前外部用户在目标应用下创建 ACP 会话（会话归属 = 外部用户 id，与对话面同口径）.
    /// </summary>
    private async Task HandleSessionNewAsync(HttpContext context, Guid appId, JsonElement id, CancellationToken cancellationToken)
    {
        var externalId = GetExternalId(context);
        var app = await _databaseContext.Apps
            .Where(x => x.Id == appId)
            .Select(x => new { x.TeamId })
            .FirstOrDefaultAsync(cancellationToken);
        if (app == null)
        {
            await WriteJsonAsync(context, JsonRpcError(id, -32000, "应用不存在."));
            return;
        }

        var session = new AppAgentSessionEntity
        {
            Id = Guid.CreateVersion7(),
            TeamId = app.TeamId,
            AppId = appId,
            Title = AppAgentConstants.DefaultSessionTitle,
            UserType = (int)UserType.External,
            LastMessageTime = DateTimeOffset.Now,
            CreateUserId = externalId,
        };
        _databaseContext.AppAgentSessions.Add(session);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        await WriteJsonAsync(context, JsonRpcResult(id, new JsonObject { ["sessionId"] = session.Id.ToString() }));
    }

    /// <summary>
    /// session/load：校验会话存在且归属当前凭证（跨归属一律「会话不存在」，不泄露存在性），成功返回空结果.
    /// </summary>
    private async Task HandleSessionLoadAsync(HttpContext context, Guid appId, JsonElement id, JsonElement paramsNode, CancellationToken cancellationToken)
    {
        var externalId = GetExternalId(context);
        if (!TryGetSessionId(paramsNode, out var sessionId))
        {
            await WriteJsonAsync(context, JsonRpcError(id, -32602, "Invalid params: sessionId is required."));
            return;
        }

        var row = await FindOwnedSessionAsync(sessionId, appId, externalId, cancellationToken);
        if (row == null)
        {
            await WriteJsonAsync(context, JsonRpcError(id, -32000, "会话不存在."));
            return;
        }

        await WriteJsonAsync(context, JsonRpcResult(id, new JsonObject()));
    }

    /// <summary>
    /// session/prompt：执行一轮应用对话，SSE 流式下发 session/update 通知，最终 response 携带 stopReason 收口.
    /// </summary>
    private async Task HandlePromptAsync(HttpContext context, Guid appId, JsonElement id, JsonElement paramsNode, CancellationToken cancellationToken)
    {
        var externalId = GetExternalId(context);
        if (!TryGetSessionId(paramsNode, out var sessionId))
        {
            await WriteJsonAsync(context, JsonRpcError(id, -32602, "Invalid params: sessionId is required."));
            return;
        }

        var prompt = ExtractPromptText(paramsNode);
        if (prompt == null)
        {
            await WriteJsonAsync(context, JsonRpcError(id, -32602, "Invalid params: prompt must be an array of text content blocks."));
            return;
        }

        var row = await FindOwnedSessionAsync(sessionId, appId, externalId, cancellationToken);
        if (row == null)
        {
            await WriteJsonAsync(context, JsonRpcError(id, -32000, "会话不存在."));
            return;
        }

        AIAgent agent;
        AgentSession session;
        try
        {
            agent = await _agentFactory.CreateAsync(appId, row.TeamId, externalId, sessionId, isDebug: false, promptId: 0, cancellationToken);
            session = await LoadSessionAsync(agent, sessionId, cancellationToken);
        }
        catch (BusinessException ex)
        {
            await WriteJsonAsync(context, JsonRpcError(id, -32000, ex.Message));
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ACP 会话装配失败，appId={AppId} sessionId={SessionId}.", appId, sessionId);
            await WriteJsonAsync(context, JsonRpcError(id, -32603, "Internal error."));
            return;
        }

        using var runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (!_runRegistry.TryRegister(sessionId, runCancellation))
        {
            await WriteJsonAsync(context, JsonRpcError(id, -32000, "该会话已有进行中的对话，请稍后重试."));
            return;
        }

        try
        {
            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "text/event-stream; charset=utf-8";
            context.Response.Headers.CacheControl = "no-cache";

            var mapper = new PromptUpdateMapper(sessionId);
            var stopReason = "end_turn";
            int? errorCode = null;
            string? errorMessage = null;

            try
            {
                var messages = new List<ChatMessage> { new(ChatRole.User, prompt) };
                await foreach (var update in agent.RunStreamingAsync(messages, session, cancellationToken: runCancellation.Token))
                {
                    foreach (var notification in mapper.Map(update))
                    {
                        await WriteSseAsync(context, notification, cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // session/cancel 中断：保留已产出内容，按协议返回 cancelled
                stopReason = "cancelled";
            }
            catch (OperationCanceledException)
            {
                // 客户端断开：尽力持久化后直接结束，无法再写响应
                await SaveSessionSafeAsync(agent, sessionId, session);
                return;
            }
            catch (BusinessException ex)
            {
                errorCode = -32000;
                errorMessage = ex.Message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ACP 对话执行失败，appId={AppId} sessionId={SessionId}.", appId, sessionId);
                errorCode = -32603;
                errorMessage = "Internal error.";
            }

            if (errorCode == null || stopReason == "cancelled")
            {
                await SaveSessionSafeAsync(agent, sessionId, session);
            }

            JsonNode finalMessage = errorCode != null && stopReason != "cancelled"
                ? JsonRpcError(id, errorCode.Value, errorMessage!)
                : JsonRpcResult(id, new JsonObject { ["stopReason"] = stopReason });
            await WriteSseAsync(context, finalMessage, CancellationToken.None);
        }
        finally
        {
            _runRegistry.Unregister(sessionId, runCancellation);
        }
    }

    /// <summary>
    /// session/cancel：取消该会话进行中的一轮对话（幂等）.
    /// </summary>
    private void TryCancel(JsonElement paramsNode)
    {
        if (TryGetSessionId(paramsNode, out var sessionId))
        {
            _runRegistry.TryCancel(sessionId);
        }
    }

    private async Task<AppAgentSessionEntity?> FindOwnedSessionAsync(Guid sessionId, Guid appId, long externalId, CancellationToken cancellationToken)
    {
        return await _databaseContext.AppAgentSessions
            .Where(x => x.Id == sessionId && x.AppId == appId && x.CreateUserId == externalId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static long GetExternalId(HttpContext context)
    {
        var value = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return long.TryParse(value, out var id) ? id : 0;
    }

    private static bool TryGetSessionId(JsonElement paramsNode, out Guid sessionId)
    {
        sessionId = Guid.Empty;
        return paramsNode.ValueKind == JsonValueKind.Object
            && paramsNode.TryGetProperty("sessionId", out var node)
            && node.ValueKind == JsonValueKind.String
            && Guid.TryParse(node.GetString(), out sessionId)
            && sessionId != Guid.Empty;
    }

    /// <summary>
    /// 提取 prompt 文本：仅支持 text 内容块（图片/音频/资源块不支持），拼接为一条用户消息.
    /// </summary>
    private static string? ExtractPromptText(JsonElement paramsNode)
    {
        if (paramsNode.ValueKind != JsonValueKind.Object
            || !paramsNode.TryGetProperty("prompt", out var promptNode)
            || promptNode.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var builder = new StringBuilder();
        foreach (var block in promptNode.EnumerateArray())
        {
            if (block.ValueKind != JsonValueKind.Object
                || !block.TryGetProperty("type", out var typeNode)
                || typeNode.ValueKind != JsonValueKind.String
                || typeNode.GetString() != "text"
                || !block.TryGetProperty("text", out var textNode)
                || textNode.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            builder.AppendLine(textNode.GetString());
        }

        var text = builder.ToString().Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    /// <summary>
    /// 加载会话热态快照（与飞书渠道同一套加载逻辑）.
    /// </summary>
    private async Task<AgentSession> LoadSessionAsync(AIAgent agent, Guid sessionId, CancellationToken cancellationToken)
    {
        AgentSession session;
        var snapshot = await _hotStore.GetSessionSnapshotAsync(sessionId, cancellationToken);
        if (!string.IsNullOrEmpty(snapshot))
        {
            using var document = JsonDocument.Parse(snapshot);
            session = await agent.DeserializeSessionAsync(document.RootElement.Clone(), cancellationToken: cancellationToken);
        }
        else
        {
            session = await agent.CreateSessionAsync(cancellationToken);
        }

        session.StateBag.SetValue(AppAgentConstants.SessionIdStateKey, sessionId.ToString());
        return session;
    }

    /// <summary>
    /// 持久化会话热态快照并触发落库；持久化失败不影响已下发的响应.
    /// </summary>
    private async Task SaveSessionSafeAsync(AIAgent agent, Guid sessionId, AgentSession session)
    {
        try
        {
            var serialized = await agent.SerializeSessionAsync(session, cancellationToken: CancellationToken.None);
            await _hotStore.SetSessionSnapshotAsync(sessionId, serialized.GetRawText(), CancellationToken.None);
            await _flushService.FlushAsync(sessionId, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ACP 会话持久化失败，sessionId={SessionId}.", sessionId);
        }
    }

    private static JsonObject JsonRpcResult(JsonElement id, JsonNode result)
    {
        return new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = JsonSerializer.SerializeToNode(id, JsonOptions),
            ["result"] = result,
        };
    }

    private static JsonObject JsonRpcError(JsonElement? id, int code, string message)
    {
        return new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id == null ? null : JsonSerializer.SerializeToNode(id.Value, JsonOptions),
            ["error"] = new JsonObject
            {
                ["code"] = code,
                ["message"] = message,
            },
        };
    }

    private static async Task WriteJsonAsync(HttpContext context, JsonNode message)
    {
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "application/json; charset=utf-8";
        await context.Response.WriteAsync(message.ToJsonString(JsonOptions), CancellationToken.None);
    }

    private static async Task WriteSseAsync(HttpContext context, JsonNode message, CancellationToken cancellationToken)
    {
        await context.Response.WriteAsync($"data: {message.ToJsonString(JsonOptions)}\n\n", cancellationToken);
        await context.Response.Body.FlushAsync(cancellationToken);
    }

    /// <summary>
    /// 单轮运行的流式内容映射器：AgentResponseUpdate 内容映射为 ACP session/update 通知
    /// （文本→agent_message_chunk、推理→agent_thought_chunk、工具调用→tool_call/tool_call_update、
    /// 流程过程 DataContent→节点 tool_call 序列）.
    /// </summary>
    private sealed class PromptUpdateMapper
    {
        private readonly Guid _sessionId;
        private readonly HashSet<string> _seenNodes = new(StringComparer.Ordinal);

        public PromptUpdateMapper(Guid sessionId)
        {
            _sessionId = sessionId;
        }

        public IEnumerable<JsonObject> Map(AgentResponseUpdate update)
        {
            foreach (var content in update.Contents)
            {
                switch (content)
                {
                    case TextContent text when !string.IsNullOrEmpty(text.Text):
                        yield return MessageChunk(text.Text);
                        break;

                    case TextReasoningContent thought when !string.IsNullOrEmpty(thought.Text):
                        yield return ThoughtChunk(thought.Text);
                        break;

                    case FunctionCallContent call:
                        yield return ToolCall(call);
                        break;

                    case FunctionResultContent result:
                        yield return ToolCallUpdate(result);
                        break;

                    case DataContent data when data.MediaType == WorkflowChatStreamContract.DataMediaType:
                        foreach (var notification in MapWorkflowData(data))
                        {
                            yield return notification;
                        }

                        break;
                }
            }
        }

        /// <summary>
        /// 流程过程负载映射：节点状态→tool_call/tool_call_update 序列（AI 节点文本增量已作为正文下发），
        /// started/completed 事件冗余不下发，suspended 下发 thought 片段.
        /// </summary>
        private IEnumerable<JsonObject> MapWorkflowData(DataContent data)
        {
            JsonElement payload;
            try
            {
                payload = JsonSerializer.Deserialize<JsonElement>(data.Data.Span);
            }
            catch (JsonException)
            {
                yield break;
            }

            if (payload.ValueKind != JsonValueKind.Object || !payload.TryGetProperty("event", out var eventNode))
            {
                yield break;
            }

            var @event = eventNode.GetString();
            if (@event == "node"
                && payload.TryGetProperty("nodeKey", out var nodeKeyNode) && nodeKeyNode.ValueKind == JsonValueKind.String
                && payload.TryGetProperty("nodeState", out var nodeStateNode) && nodeStateNode.ValueKind == JsonValueKind.String)
            {
                var nodeKey = nodeKeyNode.GetString()!;
                var nodeName = payload.TryGetProperty("nodeName", out var nodeNameNode) && nodeNameNode.ValueKind == JsonValueKind.String
                    ? nodeNameNode.GetString()!
                    : nodeKey;
                var status = MapNodeState(nodeStateNode.GetString()!);
                if (status == null)
                {
                    yield break;
                }

                var toolCallId = $"node:{nodeKey}";
                if (_seenNodes.Add(toolCallId))
                {
                    yield return Update(new JsonObject
                    {
                        ["sessionUpdate"] = "tool_call",
                        ["toolCallId"] = toolCallId,
                        ["title"] = nodeName,
                        ["kind"] = "other",
                        ["status"] = status,
                    });
                }
                else
                {
                    yield return Update(new JsonObject
                    {
                        ["sessionUpdate"] = "tool_call_update",
                        ["toolCallId"] = toolCallId,
                        ["status"] = status,
                    });
                }
            }
            else if (@event == "suspended"
                && payload.TryGetProperty("message", out var messageNode) && messageNode.ValueKind == JsonValueKind.String
                && !string.IsNullOrEmpty(messageNode.GetString()))
            {
                yield return ThoughtChunk(messageNode.GetString()!);
            }
        }

        private static string? MapNodeState(string nodeState)
        {
            return nodeState switch
            {
                "pending" => "pending",
                "running" => "in_progress",
                "completed" => "completed",
                "skipped" => "completed",
                "failed" => "failed",
                _ => null,
            };
        }

        private JsonObject ToolCall(FunctionCallContent call)
        {
            var node = new JsonObject
            {
                ["sessionUpdate"] = "tool_call",
                ["toolCallId"] = string.IsNullOrEmpty(call.CallId) ? Guid.NewGuid().ToString("N") : call.CallId,
                ["title"] = string.IsNullOrEmpty(call.Name) ? "tool" : call.Name,
                ["kind"] = "other",
                ["status"] = "in_progress",
            };

            if (call.Arguments != null)
            {
                try
                {
                    node["rawInput"] = JsonSerializer.SerializeToNode(call.Arguments, JsonOptions);
                }
                catch (NotSupportedException)
                {
                    // 参数含不可序列化对象时省略 rawInput，不影响通知本身
                }
            }

            return Update(node);
        }

        private JsonObject ToolCallUpdate(FunctionResultContent result)
        {
            var node = new JsonObject
            {
                ["sessionUpdate"] = "tool_call_update",
                ["toolCallId"] = string.IsNullOrEmpty(result.CallId) ? Guid.NewGuid().ToString("N") : result.CallId,
                ["status"] = "completed",
            };

            if (result.Result != null)
            {
                try
                {
                    node["rawOutput"] = JsonSerializer.SerializeToNode(result.Result, JsonOptions);
                }
                catch (NotSupportedException)
                {
                    // 同 rawInput：无法序列化时省略
                }
            }

            return Update(node);
        }

        private JsonObject MessageChunk(string text)
        {
            return Update(new JsonObject
            {
                ["sessionUpdate"] = "agent_message_chunk",
                ["content"] = TextBlock(text),
            });
        }

        private JsonObject ThoughtChunk(string text)
        {
            return Update(new JsonObject
            {
                ["sessionUpdate"] = "agent_thought_chunk",
                ["content"] = TextBlock(text),
            });
        }

        private static JsonObject TextBlock(string text)
        {
            return new JsonObject
            {
                ["type"] = "text",
                ["text"] = text,
            };
        }

        private JsonObject Update(JsonObject update)
        {
            return new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["method"] = "session/update",
                ["params"] = new JsonObject
                {
                    ["sessionId"] = _sessionId.ToString(),
                    ["update"] = update,
                },
            };
        }
    }
}

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

namespace MoAI.AI.A2a;

/// <summary>
/// 应用 A2A（Google Agent2Agent）服务端：把应用 Agent（Agent 应用 / Workflow 应用）以 A2A 协议
/// 暴露给外部 agent 调用，会话与执行管线与 AG-UI 对话完全同源（<see cref="AppAgentFactory"/> 装配 +
/// 热态快照 + 落库压缩）。
/// <para>传输为 JSON-RPC 2.0 over HTTP（POST 单条请求）。协议映射：<c>message/send</c> 同步执行一轮对话
/// 返回最终 Task（artifacts 携带回复文本）；<c>message/stream</c> 以 text/event-stream 下发
/// Task（working）/ TaskArtifactUpdateEvent（文本增量）/ TaskStatusUpdateEvent（completed/failed/canceled，
/// final）事件并以最终完整 Task 收口；<c>tasks/get</c> 查询任务（进程内尽力而为）；<c>tasks/cancel</c> 中断；
/// <c>contextId</c> 承载 MoAI 会话 id（缺省自动建会话，携带即续聊）。
/// 认证与门禁（app_a2a 范围、团队归属、已发布应用、外部用户语义）由 ExternalAuthenticationMiddleware 前置完成；
/// Agent Card 见 <c>GET /api/external/app/{"{appId}"}/a2a/agent.json</c>.</para>
/// </summary>
[InjectOnScoped]
public sealed class AppA2aServer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly DatabaseContext _databaseContext;
    private readonly AppAgentFactory _agentFactory;
    private readonly AppChatHotStore _hotStore;
    private readonly AppChatFlushService _flushService;
    private readonly AppA2aTaskRegistry _taskRegistry;
    private readonly ILogger<AppA2aServer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppA2aServer"/> class.
    /// </summary>
    public AppA2aServer(DatabaseContext databaseContext, AppAgentFactory agentFactory, AppChatHotStore hotStore, AppChatFlushService flushService, AppA2aTaskRegistry taskRegistry, ILogger<AppA2aServer> logger)
    {
        _databaseContext = databaseContext;
        _agentFactory = agentFactory;
        _hotStore = hotStore;
        _flushService = flushService;
        _taskRegistry = taskRegistry;
        _logger = logger;
    }

    /// <summary>
    /// 输出 Agent Card（A2A 发现：GET /a2a/agent.json，鉴权与门禁已由中间件完成）.
    /// </summary>
    /// <param name="context">HTTP 上下文.</param>
    /// <param name="appId">路由应用 id.</param>
    public async Task HandleAgentCardAsync(HttpContext context, Guid appId)
    {
        var app = await _databaseContext.Apps
            .AsNoTracking()
            .Where(x => x.Id == appId)
            .Select(x => new { x.Name, x.Description })
            .FirstOrDefaultAsync(context.RequestAborted);
        if (app == null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsJsonAsync(new { error = "Application not found." }, context.RequestAborted);
            return;
        }

        var baseUrl = $"{context.Request.Scheme}://{context.Request.Host.Value?.TrimEnd('/')}";
        var card = new JsonObject
        {
            ["name"] = app.Name,
            ["description"] = string.IsNullOrEmpty(app.Description) ? $"MoAI application {app.Name}" : app.Description,
            ["url"] = $"{baseUrl}/api/external/app/{appId}/a2a",
            ["protocolVersion"] = "0.3.0",
            ["version"] = "1.0.0",
            ["capabilities"] = new JsonObject
            {
                ["streaming"] = true,
                ["pushNotifications"] = false,
            },
            ["defaultInputModes"] = new JsonArray("text/plain"),
            ["defaultOutputModes"] = new JsonArray("text/plain"),
            ["skills"] = new JsonArray(new JsonObject
            {
                ["id"] = appId.ToString(),
                ["name"] = app.Name,
                ["description"] = string.IsNullOrEmpty(app.Description) ? $"对话 MoAI 应用 {app.Name}" : app.Description,
                ["tags"] = new JsonArray("moai", "chat"),
            }),
        };

        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "application/json; charset=utf-8";
        await context.Response.WriteAsync(card.ToJsonString(JsonOptions), context.RequestAborted);
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

            if (method == "message/send" || method == "message/stream")
            {
                if (!hasId)
                {
                    context.Response.StatusCode = StatusCodes.Status202Accepted;
                    return;
                }

                var streaming = method == "message/stream";
                await HandleMessageAsync(context, appId, id.Value, paramsNode, streaming, cancellationToken);
                return;
            }

            if (!hasId)
            {
                context.Response.StatusCode = StatusCodes.Status202Accepted;
                return;
            }

            switch (method)
            {
                case "tasks/get":
                    await HandleTasksGetAsync(context, appId, id.Value, paramsNode);
                    return;

                case "tasks/cancel":
                    await HandleTasksCancelAsync(context, appId, id.Value, paramsNode);
                    return;

                default:
                    await WriteJsonAsync(context, JsonRpcError(id.Value, -32601, $"Method not found: {method}."));
                    return;
            }
        }
    }

    /// <summary>
    /// message/send（同步）与 message/stream（SSE）：执行一轮应用对话并以 Task 承载结果.
    /// </summary>
    private async Task HandleMessageAsync(HttpContext context, Guid appId, JsonElement id, JsonElement paramsNode, bool streaming, CancellationToken cancellationToken)
    {
        var externalId = GetExternalId(context);
        var message = paramsNode.ValueKind == JsonValueKind.Object && paramsNode.TryGetProperty("message", out var messageNode)
            ? messageNode
            : default;
        var prompt = ExtractPromptText(message);
        if (prompt == null)
        {
            await WriteJsonAsync(context, JsonRpcError(id, -32602, "Invalid params: message.parts must contain text parts."));
            return;
        }

        // contextId 承载 MoAI 会话：缺省自动建会话；携带则续聊（须归属当前凭证）
        Guid sessionId;
        var contextId = message.ValueKind == JsonValueKind.Object
            && message.TryGetProperty("contextId", out var contextIdNode)
            && contextIdNode.ValueKind == JsonValueKind.String
            && Guid.TryParse(contextIdNode.GetString(), out var parsedContextId)
            && parsedContextId != Guid.Empty
            ? parsedContextId
            : (Guid?)null;
        if (contextId == null)
        {
            sessionId = await CreateSessionAsync(appId, externalId, cancellationToken);
        }
        else
        {
            var owned = await _databaseContext.AppAgentSessions
                .AnyAsync(x => x.Id == contextId.Value && x.AppId == appId && x.CreateUserId == externalId, cancellationToken);
            if (!owned)
            {
                await WriteJsonAsync(context, JsonRpcError(id, -32000, "会话不存在."));
                return;
            }

            sessionId = contextId.Value;
        }

        var taskId = Guid.CreateVersion7().ToString();
        var record = new AppA2aTaskRecord
        {
            TaskId = taskId,
            ContextId = sessionId,
            AppId = appId,
            ExternalId = externalId,
            State = AppA2aTaskState.Submitted,
        };
        _taskRegistry.Add(record);

        AIAgent agent;
        AgentSession session;
        try
        {
            agent = await _agentFactory.CreateAsync(appId, await ResolveTeamIdAsync(appId, cancellationToken), externalId, sessionId, isDebug: false, promptId: 0, cancellationToken);
            session = await LoadSessionAsync(agent, sessionId, cancellationToken);
        }
        catch (BusinessException ex)
        {
            record.State = AppA2aTaskState.Failed;
            record.StatusMessage = ex.Message;
            await WriteJsonAsync(context, JsonRpcError(id, -32000, ex.Message));
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "A2A 会话装配失败，appId={AppId} sessionId={SessionId}.", appId, sessionId);
            record.State = AppA2aTaskState.Failed;
            record.StatusMessage = "Internal error.";
            await WriteJsonAsync(context, JsonRpcError(id, -32603, "Internal error."));
            return;
        }

        using var runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (!_taskRegistry.TryRegisterRun(taskId, runCancellation))
        {
            record.State = AppA2aTaskState.Failed;
            record.StatusMessage = "任务重复提交.";
            await WriteJsonAsync(context, JsonRpcError(id, -32000, "任务重复提交，请稍后重试."));
            return;
        }

        try
        {
            if (!streaming)
            {
                // message/send：同步执行，最终 Task 一次性返回
                record.State = AppA2aTaskState.Working;
                try
                {
                    var reply = await RunToTextAsync(agent, sessionId, session, prompt, runCancellation.Token);
                    record.Artifacts.Add(reply);
                    record.State = AppA2aTaskState.Completed;
                    await WriteJsonAsync(context, JsonRpcResult(id, BuildTask(record)));
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    record.State = AppA2aTaskState.Canceled;
                    await SaveSessionSafeAsync(agent, sessionId, session);
                    await WriteJsonAsync(context, JsonRpcResult(id, BuildTask(record)));
                }
                catch (BusinessException ex)
                {
                    record.State = AppA2aTaskState.Failed;
                    record.StatusMessage = ex.Message;
                    await SaveSessionSafeAsync(agent, sessionId, session);
                    await WriteJsonAsync(context, JsonRpcResult(id, BuildTask(record)));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "A2A 对话执行失败，appId={AppId} sessionId={SessionId}.", appId, sessionId);
                    record.State = AppA2aTaskState.Failed;
                    record.StatusMessage = "Internal error.";
                    await WriteJsonAsync(context, JsonRpcResult(id, BuildTask(record)));
                }

                return;
            }

            // message/stream：SSE 下发 working Task → 文本增量 artifact 事件 → completed/failed/canceled 收口 + 最终 Task
            record.State = AppA2aTaskState.Working;
            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "text/event-stream; charset=utf-8";
            context.Response.Headers.CacheControl = "no-cache";

            try
            {
                await WriteSseAsync(context, JsonRpcResult(id, BuildTask(record)), cancellationToken);
                var builder = new StringBuilder();
                await foreach (var update in agent.RunStreamingAsync(new List<ChatMessage> { new(ChatRole.User, prompt) }, session, cancellationToken: runCancellation.Token))
                {
                    foreach (var content in update.Contents)
                    {
                        if (content is TextContent text && !string.IsNullOrEmpty(text.Text))
                        {
                            builder.Append(text.Text);
                            record.Artifacts.Add(text.Text);
                            await WriteSseAsync(context, JsonRpcResult(id, BuildArtifactEvent(record, builder.ToString(), lastChunk: false)), cancellationToken);
                        }
                    }
                }

                record.State = AppA2aTaskState.Completed;
                await WriteSseAsync(context, JsonRpcResult(id, BuildArtifactEvent(record, builder.ToString(), lastChunk: true)), cancellationToken);
                await WriteSseAsync(context, JsonRpcResult(id, BuildStatusEvent(record, final: true)), cancellationToken);
                await SaveSessionSafeAsync(agent, sessionId, session);
                await WriteSseAsync(context, JsonRpcResult(id, BuildTask(record)), CancellationToken.None);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                record.State = AppA2aTaskState.Canceled;
                await SaveSessionSafeAsync(agent, sessionId, session);
                await WriteSseAsync(context, JsonRpcResult(id, BuildStatusEvent(record, final: true)), CancellationToken.None);
                await WriteSseAsync(context, JsonRpcResult(id, BuildTask(record)), CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                await SaveSessionSafeAsync(agent, sessionId, session);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "A2A 流式对话执行失败，appId={AppId} sessionId={SessionId}.", appId, sessionId);
                record.State = AppA2aTaskState.Failed;
                record.StatusMessage = "Internal error.";
                await WriteSseAsync(context, JsonRpcResult(id, BuildStatusEvent(record, final: true)), CancellationToken.None);
                await WriteSseAsync(context, JsonRpcResult(id, BuildTask(record)), CancellationToken.None);
            }
        }
        finally
        {
            _taskRegistry.UnregisterRun(taskId, runCancellation);
        }
    }

    /// <summary>
    /// tasks/get：返回任务当前状态（进程内尽力而为；跨归属一律任务不存在）.
    /// </summary>
    private async Task HandleTasksGetAsync(HttpContext context, Guid appId, JsonElement id, JsonElement paramsNode)
    {
        var externalId = GetExternalId(context);
        if (!TryGetTaskId(paramsNode, out var taskId))
        {
            await WriteJsonAsync(context, JsonRpcError(id, -32602, "Invalid params: id is required."));
            return;
        }

        await Task.CompletedTask;
        var record = _taskRegistry.Find(taskId, appId, externalId);
        if (record == null)
        {
            await WriteJsonAsync(context, JsonRpcError(id, -32001, "任务不存在."));
            return;
        }

        await WriteJsonAsync(context, JsonRpcResult(id, BuildTask(record)));
    }

    /// <summary>
    /// tasks/cancel：中断进行中的一轮对话并把任务置为 canceled（幂等）.
    /// </summary>
    private async Task HandleTasksCancelAsync(HttpContext context, Guid appId, JsonElement id, JsonElement paramsNode)
    {
        var externalId = GetExternalId(context);
        if (!TryGetTaskId(paramsNode, out var taskId))
        {
            await WriteJsonAsync(context, JsonRpcError(id, -32602, "Invalid params: id is required."));
            return;
        }

        await Task.CompletedTask;
        var record = _taskRegistry.TryCancel(taskId, appId, externalId);
        if (record == null)
        {
            await WriteJsonAsync(context, JsonRpcError(id, -32001, "任务不存在."));
            return;
        }

        await WriteJsonAsync(context, JsonRpcResult(id, BuildTask(record)));
    }

    /// <summary>
    /// 执行一轮对话并聚合为完整回复文本（message/send 同步模式）.
    /// </summary>
    private async Task<string> RunToTextAsync(AIAgent agent, Guid sessionId, AgentSession session, string prompt, CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        await foreach (var update in agent.RunStreamingAsync(new List<ChatMessage> { new(ChatRole.User, prompt) }, session, cancellationToken: cancellationToken))
        {
            foreach (var content in update.Contents)
            {
                if (content is TextContent text && !string.IsNullOrEmpty(text.Text))
                {
                    builder.Append(text.Text);
                }
            }
        }

        await SaveSessionSafeAsync(agent, sessionId, session);
        return builder.ToString();
    }

    /// <summary>
    /// message/send / message/stream 的上下文准备：返回应用归属团队 id.
    /// </summary>
    private async Task<int> ResolveTeamIdAsync(Guid appId, CancellationToken cancellationToken)
    {
        var teamId = await _databaseContext.Apps
            .Where(x => x.Id == appId)
            .Select(x => (int?)x.TeamId)
            .FirstOrDefaultAsync(cancellationToken);
        return teamId ?? 0;
    }

    /// <summary>
    /// 为当前外部用户在目标应用下创建 A2A 会话（会话归属 = 外部用户 id，与对话面同口径）.
    /// </summary>
    private async Task<Guid> CreateSessionAsync(Guid appId, long externalId, CancellationToken cancellationToken)
    {
        var teamId = await ResolveTeamIdAsync(appId, cancellationToken);
        var session = new AppAgentSessionEntity
        {
            Id = Guid.CreateVersion7(),
            TeamId = teamId,
            AppId = appId,
            Title = AppAgentConstants.DefaultSessionTitle,
            UserType = (int)UserType.External,
            LastMessageTime = DateTimeOffset.Now,
            CreateUserId = externalId,
        };
        _databaseContext.AppAgentSessions.Add(session);
        await _databaseContext.SaveChangesAsync(cancellationToken);
        return session.Id;
    }

    private static long GetExternalId(HttpContext context)
    {
        var value = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return long.TryParse(value, out var id) ? id : 0;
    }

    private static bool TryGetTaskId(JsonElement paramsNode, out string? taskId)
    {
        taskId = null;
        if (paramsNode.ValueKind == JsonValueKind.Object
            && paramsNode.TryGetProperty("id", out var node)
            && node.ValueKind == JsonValueKind.String)
        {
            taskId = node.GetString();
        }

        return !string.IsNullOrEmpty(taskId);
    }

    /// <summary>
    /// 提取 message.parts 中的文本（A2A part：{"kind":"text","text":…}，兼容 {"type":"text"} 旧形态），拼接为一条用户消息.
    /// </summary>
    private static string? ExtractPromptText(JsonElement message)
    {
        if (message.ValueKind != JsonValueKind.Object
            || !message.TryGetProperty("parts", out var partsNode)
            || partsNode.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var builder = new StringBuilder();
        foreach (var part in partsNode.EnumerateArray())
        {
            if (part.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var kind = part.TryGetProperty("kind", out var kindNode) && kindNode.ValueKind == JsonValueKind.String
                ? kindNode.GetString()
                : part.TryGetProperty("type", out var typeNode) && typeNode.ValueKind == JsonValueKind.String
                    ? typeNode.GetString()
                    : null;
            if (kind != "text"
                || !part.TryGetProperty("text", out var textNode)
                || textNode.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            builder.AppendLine(textNode.GetString());
        }

        var text = builder.ToString().Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    /// <summary>
    /// 加载会话热态快照（与 ACP/飞书渠道同一套加载逻辑）.
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
            _logger.LogError(ex, "A2A 会话持久化失败，sessionId={SessionId}.", sessionId);
        }
    }

    /// <summary>
    /// 组装 A2A Task（kind=task；artifacts 承载回复文本）.
    /// </summary>
    private static JsonObject BuildTask(AppA2aTaskRecord record)
    {
        var task = new JsonObject
        {
            ["id"] = record.TaskId,
            ["contextId"] = record.ContextId.ToString(),
            ["kind"] = "task",
            ["status"] = new JsonObject
            {
                ["state"] = record.State.ToString().ToLowerInvariant(),
                ["timestamp"] = record.UpdatedTime.ToString("o"),
            },
        };
        if (record.Artifacts.Count > 0)
        {
            task["artifacts"] = new JsonArray(BuildTextArtifact(record.Artifacts));
        }

        if (!string.IsNullOrEmpty(record.StatusMessage) && record.State == AppA2aTaskState.Failed)
        {
            task["status"]["message"] = new JsonObject
            {
                ["role"] = "agent",
                ["parts"] = new JsonArray(BuildTextPart(record.StatusMessage)),
                ["kind"] = "message",
            };
        }

        return task;
    }

    private static JsonObject BuildTextArtifact(IReadOnlyList<string> pieces)
    {
        var text = string.Concat(pieces);
        return new JsonObject
        {
            ["artifactId"] = Guid.NewGuid().ToString(),
            ["name"] = "response",
            ["parts"] = new JsonArray(BuildTextPart(text)),
        };
    }

    private static JsonObject BuildTextPart(string text)
    {
        return new JsonObject
        {
            ["kind"] = "text",
            ["text"] = text,
        };
    }

    /// <summary>
    /// 组装 TaskStatusUpdateEvent（stream 事件；final=true 表示该任务不再有后续事件）.
    /// </summary>
    private static JsonObject BuildStatusEvent(AppA2aTaskRecord record, bool final)
    {
        return new JsonObject
        {
            ["kind"] = "status-update",
            ["taskId"] = record.TaskId,
            ["contextId"] = record.ContextId.ToString(),
            ["status"] = new JsonObject
            {
                ["state"] = record.State.ToString().ToLowerInvariant(),
                ["timestamp"] = record.UpdatedTime.ToString("o"),
            },
            ["final"] = final,
        };
    }

    /// <summary>
    /// 组装 TaskArtifactUpdateEvent（stream 文本增量；append=true、lastChunk 标记收尾）.
    /// </summary>
    private static JsonObject BuildArtifactEvent(AppA2aTaskRecord record, string text, bool lastChunk)
    {
        return new JsonObject
        {
            ["kind"] = "artifact-update",
            ["taskId"] = record.TaskId,
            ["contextId"] = record.ContextId.ToString(),
            ["append"] = true,
            ["lastChunk"] = lastChunk,
            ["artifact"] = new JsonObject
            {
                ["artifactId"] = "response",
                ["parts"] = new JsonArray(BuildTextPart(text)),
            },
        };
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
}

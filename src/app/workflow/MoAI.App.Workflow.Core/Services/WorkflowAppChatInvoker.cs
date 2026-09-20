using System.Text.Json.Nodes;
using Maomi;
using Microsoft.Agents.AI.Compaction;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using MoAI.AI.Services;
using MoAI.App.Workflow.Definition;
using MoAI.App.Workflow.Instance;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;

namespace MoAI.App.Workflow.Services;

/// <summary>
/// <inheritdoc cref="IWorkflowAppChatInvoker"/>
/// 一轮对话 = 一次流程执行：
/// 1. 校验应用为流程应用（禁用/类型）；
/// 2. 定义选择：工作台「调试」Tab（UseDraft）按最新草稿执行（免发布，仅团队管理员，实例记为调试）；正式对话按已发布快照；
/// 3. 用户消息作为启动参数 <c>question</c> 驱动流程（开始节点固定 question 字段；同时镜像 <c>query</c> 兼容旧发布流程）；
/// 4. 注入 sys.* 对话上下文（userId/appId/conversationId/messageId/history），
///    history 单独走 MAF 压缩组件（CompactionProvider + 应用 execution_settings 策略），不依赖 Agent 管线；
/// 5. 从结束节点输出提取回复文本（reply/answer/output/text/result 优先，单一字符串属性次之，否则整体序列化）.
/// </summary>
[InjectOnScoped]
public class WorkflowAppChatInvoker : IWorkflowAppChatInvoker
{
    /// <summary>压缩前从库内加载的最大消息条数（限制内存与压缩耗时）.</summary>
    private const int HistoryLoadLimit = 200;

    /// <summary>压缩失败退回时的兜底条数（与旧版行为一致）.</summary>
    private const int HistoryFallbackLimit = 20;

    private static readonly string[] ReplyKeys = ["reply", "answer", "output", "text", "result"];

    private readonly DatabaseContext _databaseContext;
    private readonly WorkflowEngine _workflowEngine;
    private readonly Stores.DatabaseWorkflowDefinitionStore _definitionStore;
    private readonly WorkflowExecutionContext _executionContext;
    private readonly AppCompactionStrategyFactory _compactionStrategyFactory;
    private readonly ILogger<WorkflowAppChatInvoker> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowAppChatInvoker"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="workflowEngine">工作流引擎.</param>
    /// <param name="definitionStore">工作流定义存储.</param>
    /// <param name="executionContext">工作流执行上下文.</param>
    /// <param name="compactionStrategyFactory">上下文压缩策略工厂（MAF 压缩组件单独使用）.</param>
    /// <param name="logger">日志.</param>
    public WorkflowAppChatInvoker(
        DatabaseContext databaseContext,
        WorkflowEngine workflowEngine,
        Stores.DatabaseWorkflowDefinitionStore definitionStore,
        WorkflowExecutionContext executionContext,
        AppCompactionStrategyFactory compactionStrategyFactory,
        ILogger<WorkflowAppChatInvoker> logger)
    {
        _databaseContext = databaseContext;
        _workflowEngine = workflowEngine;
        _definitionStore = definitionStore;
        _executionContext = executionContext;
        _compactionStrategyFactory = compactionStrategyFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<WorkflowAppChatResult> InvokeAsync(WorkflowAppChatRequest request, CancellationToken cancellationToken = default)
    {
        var app = await _databaseContext.Apps.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.AppId, cancellationToken).ConfigureAwait(false);
        if (app == null || app.IsExternal || app.AppType != (int)AppType.Workflow)
        {
            throw new BusinessException("应用不存在或不是流程应用.") { StatusCode = 404 };
        }

        if (app.IsDisable)
        {
            throw new BusinessException("应用已被禁用.") { StatusCode = 403 };
        }

        var config = await _definitionStore.FindConfigEntityAsync(app.Id, cancellationToken).ConfigureAwait(false);

        // 定义选择：工作台「调试」Tab（UseDraft）按最新草稿执行（免发布，仅团队管理员）；正式对话按已发布快照
        string definitionJson;
        if (request.UseDraft)
        {
            var isManager = await _databaseContext.TeamUsers.AsNoTracking()
                .AnyAsync(x => x.TeamId == app.TeamId && x.UserId == request.UserId && x.Role >= (int)TeamRole.Admin, cancellationToken).ConfigureAwait(false);
            if (!isManager)
            {
                throw new BusinessException("仅团队管理员可按最新草稿调试流程.") { StatusCode = 403 };
            }

            definitionJson = config?.DraftDefinition ?? string.Empty;
            if (string.IsNullOrWhiteSpace(definitionJson))
            {
                throw new BusinessException("请先在「设计」中编排并保存流程.") { StatusCode = 400 };
            }
        }
        else
        {
            if (app.PublishStatus != 1 || string.IsNullOrWhiteSpace(config?.PublishedDefinition))
            {
                throw new BusinessException("流程尚未发布，无法对话.") { StatusCode = 400 };
            }

            definitionJson = config!.PublishedDefinition!;
        }

        _executionContext.TeamId = app.TeamId;
        _executionContext.AppId = request.AppId;
        _executionContext.UserId = request.UserId;
        _executionContext.ConfigId = config!.Id;
        // 草稿调试实例在运行历史中按「调试」类型记录（与设计器调试运行一致）
        _executionContext.IsDebug = request.UseDraft;

        var systemContext = new JsonObject
        {
            ["userId"] = request.UserId.ToString(),
            ["appId"] = request.AppId.ToString(),
            ["conversationId"] = request.SessionId.ToString(),
            ["messageId"] = Guid.CreateVersion7().ToString("N"),
            ["history"] = await LoadHistoryAsync(request.AppId, request.SessionId, cancellationToken).ConfigureAwait(false),
        };

        var input = new JsonObject
        {
            // 开始节点固定 question 字段；query 镜像兼容旧发布流程（绑定 start.query 的历史编排）
            ["question"] = request.Query,
            ["query"] = request.Query,
        };

        var definition = WorkflowJson.DeserializeDefinition(definitionJson);
        // 根流程标记：被 Agent 工具嵌套驱动时，内层环检测仍以最外层流程为基准（AsyncLocal，仅最外层写入）
        WorkflowInstance instance;
        using (WorkflowRootContext.Begin(request.AppId))
        {
            instance = await _workflowEngine.StartWithDefinitionAsync(
                definition,
                input,
                systemVariables: null,
                systemContext,
                Guid.CreateVersion7().ToString("N"),
                cancellationToken).ConfigureAwait(false);
        }

        if (instance.Status != InstanceStatus.Completed)
        {
            return new WorkflowAppChatResult
            {
                Success = false,
                InstanceId = instance.Id,
                ErrorMessage = instance.ErrorMessage ?? "流程执行未完成.",
            };
        }

        return new WorkflowAppChatResult
        {
            Success = true,
            InstanceId = instance.Id,
            Reply = ExtractReply(instance.Output) ?? string.Empty,
        };
    }

    /// <summary>
    /// 读取会话历史并压缩：流程应用不走 Agent 管线（无 AIContextProviders），
    /// 单独使用 MAF 压缩组件按应用 execution_settings 组装策略压缩后注入 sys.history；
    /// 压缩失败退回最近 <see cref="HistoryFallbackLimit"/> 条原文。形状为 [{role, content}].
    /// </summary>
    private async Task<JsonArray> LoadHistoryAsync(Guid appId, Guid sessionId, CancellationToken cancellationToken)
    {
        var records = await _databaseContext.AppAgentMessages.AsNoTracking()
            .Where(x => x.SessionId == sessionId)
            .OrderByDescending(x => x.Seq)
            .Select(x => new { x.Role, x.Content })
            .Take(HistoryLoadLimit)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        records.Reverse();

        var messages = records
            .Where(x => (x.Role == "user" || x.Role == "assistant") && !string.IsNullOrWhiteSpace(x.Content))
            .Select(x => new ChatMessage(x.Role == "user" ? ChatRole.User : ChatRole.Assistant, x.Content))
            .ToList();
        if (messages.Count == 0)
        {
            return [];
        }

        try
        {
            var agentConfig = await _databaseContext.AppAgentConfigs.AsNoTracking()
                .FirstOrDefaultAsync(x => x.AppId == appId, cancellationToken).ConfigureAwait(false);
            var strategy = await _compactionStrategyFactory.BuildAsync(agentConfig, cancellationToken).ConfigureAwait(false);
            var compacted = await CompactionProvider.CompactAsync(strategy, messages, _logger, cancellationToken).ConfigureAwait(false);

            return ToHistoryJson(compacted);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "流程对话历史压缩失败，退回最近 {Limit} 条原文. SessionId={SessionId}", HistoryFallbackLimit, sessionId);
            return ToHistoryJson(messages.TakeLast(HistoryFallbackLimit));
        }
    }

    /// <summary>压缩产物映射为 sys.history 数组：非 user 角色统一按 assistant 呈现（摘要等上下文文本）.</summary>
    private static JsonArray ToHistoryJson(IEnumerable<ChatMessage> messages)
    {
        var history = new JsonArray();
        foreach (var message in messages)
        {
            if (string.IsNullOrWhiteSpace(message.Text))
            {
                continue;
            }

            var role = message.Role == ChatRole.User ? "user" : "assistant";
            history.Add(new JsonObject
            {
                ["role"] = role,
                ["content"] = message.Text,
            });
        }

        return history;
    }

    /// <summary>
    /// 从结束节点输出提取回复文本：常用字符串字段优先，其次唯一字符串属性，否则整体序列化为 JSON.
    /// </summary>
    private static string? ExtractReply(JsonObject? output)
    {
        if (output == null)
        {
            return null;
        }

        foreach (var key in ReplyKeys)
        {
            if (output.TryGetPropertyValue(key, out var node)
                && node is JsonValue value
                && value.TryGetValue<string>(out var text)
                && !string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
        }

        if (output.Count == 1)
        {
            var only = output.FirstOrDefault();
            return only.Value switch
            {
                JsonValue stringValue when stringValue.TryGetValue<string>(out var text) => text,
                null => null,
                _ => only.Value!.ToJsonString(),
            };
        }

        return output.ToJsonString();
    }
}

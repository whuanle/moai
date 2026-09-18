using System.Text.Json.Nodes;
using Maomi;
using Microsoft.EntityFrameworkCore;
using MoAI.AI.Services;
using MoAI.App.Workflow.Definition;
using MoAI.App.Workflow.Instance;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;

namespace MoAI.App.Workflow.Services;

/// <summary>
/// <inheritdoc cref="IWorkflowAppChatInvoker"/>
/// 一轮对话 = 一次已发布流程执行：
/// 1. 校验应用为流程应用且已发布；
/// 2. 用户消息作为启动参数 <c>query</c> 驱动流程；
/// 3. 注入 sys.* 对话上下文（userId/appId/conversationId/messageId/history）；
/// 4. 从结束节点输出提取回复文本（reply/answer/output/text/result 优先，单一字符串属性次之，否则整体序列化）.
/// </summary>
[InjectOnScoped]
public class WorkflowAppChatInvoker : IWorkflowAppChatInvoker
{
    private const int HistoryLimit = 20;

    private static readonly string[] ReplyKeys = ["reply", "answer", "output", "text", "result"];

    private readonly DatabaseContext _databaseContext;
    private readonly WorkflowEngine _workflowEngine;
    private readonly Stores.DatabaseWorkflowDefinitionStore _definitionStore;
    private readonly WorkflowExecutionContext _executionContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowAppChatInvoker"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="workflowEngine">工作流引擎.</param>
    /// <param name="definitionStore">工作流定义存储.</param>
    /// <param name="executionContext">工作流执行上下文.</param>
    public WorkflowAppChatInvoker(
        DatabaseContext databaseContext,
        WorkflowEngine workflowEngine,
        Stores.DatabaseWorkflowDefinitionStore definitionStore,
        WorkflowExecutionContext executionContext)
    {
        _databaseContext = databaseContext;
        _workflowEngine = workflowEngine;
        _definitionStore = definitionStore;
        _executionContext = executionContext;
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

        if (app.PublishStatus != 1)
        {
            throw new BusinessException("流程尚未发布，无法对话.") { StatusCode = 400 };
        }

        var config = await _definitionStore.FindConfigEntityAsync(app.Id, cancellationToken).ConfigureAwait(false);
        if (config?.PublishedDefinition == null)
        {
            throw new BusinessException("流程尚未发布，无法对话.") { StatusCode = 400 };
        }

        _executionContext.TeamId = app.TeamId;
        _executionContext.ConfigId = config.Id;
        _executionContext.IsDebug = false;

        var systemContext = new JsonObject
        {
            ["userId"] = request.UserId.ToString(),
            ["appId"] = request.AppId.ToString(),
            ["conversationId"] = request.SessionId.ToString(),
            ["messageId"] = Guid.CreateVersion7().ToString("N"),
            ["history"] = await LoadHistoryAsync(request.SessionId, cancellationToken).ConfigureAwait(false),
        };

        var input = new JsonObject
        {
            ["query"] = request.Query,
        };

        var instance = await _workflowEngine.StartAsync(
            app.Id.ToString(),
            input,
            systemVariables: null,
            systemContext,
            Guid.CreateVersion7().ToString("N"),
            cancellationToken).ConfigureAwait(false);

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
    /// 读取会话历史消息（已落库的前序轮次，仅 user/assistant 且有内容，截取最近 <see cref="HistoryLimit"/> 条），
    /// 形状为 [{role, content}]，可直接作为 AI 对话节点的 history 输入.
    /// </summary>
    private async Task<JsonArray> LoadHistoryAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var records = await _databaseContext.AppAgentMessages.AsNoTracking()
            .Where(x => x.SessionId == sessionId)
            .OrderBy(x => x.Seq)
            .Select(x => new { x.Role, x.Content })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var history = new JsonArray();
        foreach (var record in records
            .Where(x => (x.Role == "user" || x.Role == "assistant") && !string.IsNullOrWhiteSpace(x.Content))
            .TakeLast(HistoryLimit))
        {
            history.Add(new JsonObject
            {
                ["role"] = record.Role,
                ["content"] = record.Content,
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

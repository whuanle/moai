using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Maomi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;

namespace MoAI.AI.Services;

/// <summary>
/// 流程应用工具来源：把应用绑定的本团队已发布流程应用转换为可调用工具，
/// 一次调用 = 按该流程应用的发布快照执行一轮流程，并把结束节点输出作为工具结果返回.
/// </summary>
/// <remarks>
/// 注意：<see cref="IWorkflowAppChatInvoker"/> 必须从 <see cref="IServiceProvider"/> 惰性解析，
/// 不能构造注入——其实现位于工作流模块，构造链（引擎 → 节点执行器 → 宿主 IAiChatClient →
/// AppContextProviderFactory）会回到 IEnumerable{IAppToolProvider} 本身，构造注入将形成 DI 环导致解析死锁.
/// </remarks>
[InjectOnScoped]
public sealed class WorkflowAppToolProvider : IAppToolProvider
{
    /// <summary>工具名前缀（与插件工具名隔离，避免同名冲突）.</summary>
    private const string ToolNamePrefix = "workflow__";

    /// <summary>结果序列化选项：中文不转义，减少 token 占用、便于模型阅读.</summary>
    private static readonly JsonSerializerOptions ResultSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly string[] QueryKeys = ["query", "question", "input", "text", "prompt"];

    private readonly DatabaseContext _databaseContext;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowAppToolProvider"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="serviceProvider">服务提供者（惰性解析流程应用对话执行端口，避免 DI 构造环）.</param>
    public WorkflowAppToolProvider(DatabaseContext databaseContext, IServiceProvider serviceProvider)
    {
        _databaseContext = databaseContext;
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    public int Order => 11;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AppTool>> GetToolsAsync(AppAgentBuildContext context, CancellationToken cancellationToken)
    {
        if (context.WorkflowAppIds.Count == 0)
        {
            return [];
        }

        // 绑定校验在保存时完成；此处再过滤一次，流程应用被取消发布/禁用后工具自然下线
        var apps = await _databaseContext.Apps.AsNoTracking()
            .Where(x => context.WorkflowAppIds.Contains(x.Id)
                && x.TeamId == context.TeamId
                && !x.IsExternal
                && !x.IsDisable
                && x.AppType == (int)AppType.Workflow
                && x.PublishStatus == 1)
            .Select(x => new { x.Id, x.Name, x.Description })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        if (apps.Count == 0)
        {
            return [];
        }

        // 惰性解析（见类备注）：此时作用域内各服务已构造完成，不再进入构造递归
        var workflowChatInvoker = _serviceProvider.GetRequiredService<IWorkflowAppChatInvoker>();

        var tools = new List<AppTool>();
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var app in apps)
        {
            var name = ToolNamePrefix + app.Name;
            // 同名流程应用加短 id 后缀避免工具名冲突（上游按名去重会静默丢弃后者）
            if (!usedNames.Add(name))
            {
                name = $"{name}_{app.Id.ToString("N")[..8]}";
                if (!usedNames.Add(name))
                {
                    continue;
                }
            }

            var title = string.IsNullOrWhiteSpace(app.Name) ? "流程应用" : app.Name;
            var description = string.IsNullOrWhiteSpace(app.Description)
                ? "团队流程应用工具：传入 query（要交给该流程处理的完整问题或指令），执行流程并返回结果。"
                : $"{app.Description}（传入 query 执行该流程并返回结果。）";

            var appId = app.Id;
            tools.Add(new AppTool
            {
                Name = name,
                Title = title,
                Description = description,
                Kind = "workflow",
                SourceId = appId,
                ParametersExample = "{\"query\":\"要交给该流程处理的完整问题或指令\"}",
                InvokeAsync = (argsJson, ct) => InvokeAsync(workflowChatInvoker, appId, context, argsJson, ct),
            });
        }

        return tools;
    }

    /// <summary>
    /// 执行一轮流程应用：参数取 query（兼容 question/input/text/prompt 键与纯 JSON 字符串），
    /// 未发布/执行失败等异常统一转为工具错误文本，不中断对话.
    /// </summary>
    private static async Task<AppToolResult> InvokeAsync(IWorkflowAppChatInvoker workflowChatInvoker, Guid workflowAppId, AppAgentBuildContext context, string? argsJson, CancellationToken cancellationToken)
    {
        var query = ExtractQuery(argsJson);
        if (string.IsNullOrWhiteSpace(query))
        {
            return AppToolResult.Fail("缺少 query 参数：请传入要交给该流程处理的完整问题或指令文本.");
        }

        try
        {
            // 按发布快照执行（UseDraft=false）；会话 id 透传，流程内 sys.conversationId/sys.history 与当前对话一致
            var result = await workflowChatInvoker.InvokeAsync(new WorkflowAppChatRequest
            {
                AppId = workflowAppId,
                TeamId = context.TeamId,
                UserId = context.UserId,
                SessionId = context.SessionId,
                Query = query,
                UseDraft = false,
            }, cancellationToken).ConfigureAwait(false);

            if (!result.Success)
            {
                return AppToolResult.Fail(result.ErrorMessage ?? "流程执行未完成.");
            }

            var data = JsonSerializer.Serialize(new
            {
                success = true,
                reply = result.Reply,
                instanceId = result.InstanceId,
            }, ResultSerializerOptions);
            return AppToolResult.Ok(data);
        }
#pragma warning disable CA1031 // 流程执行异常统一转为工具错误文本，避免中断对话
        catch (Exception ex)
        {
            return AppToolResult.Fail(ex.Message);
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// 从工具参数 JSON 提取流程输入：对象取 query（兼容别名键），JSON 字符串取其值，其余按原文返回.
    /// </summary>
    private static string ExtractQuery(string? argsJson)
    {
        if (string.IsNullOrWhiteSpace(argsJson))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(argsJson);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.String)
            {
                return root.GetString() ?? string.Empty;
            }

            if (root.ValueKind == JsonValueKind.Object)
            {
                foreach (var key in QueryKeys)
                {
                    if (root.TryGetProperty(key, out var value)
                        && value.ValueKind == JsonValueKind.String
                        && !string.IsNullOrWhiteSpace(value.GetString()))
                    {
                        return value.GetString()!;
                    }
                }
            }
        }
        catch (JsonException)
        {
            // 非 JSON 文本按原文作为流程输入
        }

        return argsJson.Trim();
    }
}

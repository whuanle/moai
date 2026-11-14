using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Maomi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using MoAI.Database;
using MoAI.Database.Aggregates;
using MoAI.Database.Entities;
using MoAI.Database.Enums;

namespace MoAI.AI.Services;

/// <summary>
/// Agent 应用工具来源：把应用绑定的本团队已发布 Agent 应用转换为可调用工具，
/// 一次调用 = 按该 Agent 应用的发布快照无头单轮执行（携带其自身工具链），并把最终回复作为工具结果返回；
/// 工具参数支持可选 history（调用方模型自行携带的近期对话上下文，仅本次调用内有效）.
/// </summary>
/// <remarks>
/// 注意：<see cref="IWorkflowNodeAiInvoker"/> 必须从 <see cref="IServiceProvider"/> 惰性解析，
/// 不能构造注入——其实现装配模型与工具链（经 AppContextProviderFactory）会回到
/// IEnumerable{IAppToolProvider} 本身，构造注入将形成 DI 环导致解析死锁（与 WorkflowAppToolProvider 同因）.
/// </remarks>
[InjectOnScoped]
public sealed class AgentAppToolProvider : IAppToolProvider
{
    /// <summary>工具名前缀（与流程应用工具名隔离，避免同名冲突）.</summary>
    private const string ToolNamePrefix = MoAI.AI.AppToolApprovalContract.AgentAppToolPrefix;

    /// <summary>history 参数最多携带的消息条数（截最近若干条，防超长上下文）.</summary>
    private const int MaxHistoryMessages = 10;

    private readonly DatabaseContext _databaseContext;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentAppToolProvider"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="serviceProvider">服务提供者（惰性解析 Agent 应用执行端口，避免 DI 构造环）.</param>
    public AgentAppToolProvider(DatabaseContext databaseContext, IServiceProvider serviceProvider)
    {
        _databaseContext = databaseContext;
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    public int Order => 12;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AppTool>> GetToolsAsync(AppAgentBuildContext context, CancellationToken cancellationToken)
    {
        if (context.AgentAppIds.Count == 0)
        {
            return [];
        }

        // 绑定校验在保存时完成；此处再过滤一次，Agent 应用被取消发布/禁用后工具自然下线（自身绑定在保存期已拒绝，此处兜底排除）
        var apps = await _databaseContext.Apps.AsNoTracking()
            .Where(x => context.AgentAppIds.Contains(x.Id)
                && x.TeamId == context.TeamId
                && !x.IsExternal
                && !x.IsDisable
                && x.AppType == (int)AppType.Agent
                && x.PublishStatus == 1
                && x.Id != context.AppId)
            .Select(x => new { x.Id, x.Name, x.Description })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        if (apps.Count == 0)
        {
            return [];
        }

        var tools = new List<AppTool>();
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var app in apps)
        {
            // 同名 Agent 应用加短 id 后缀避免工具名冲突（上游按名去重会静默丢弃后者）
            var name = MoAI.AI.AppResourceToolNaming.PrefixedToolName(ToolNamePrefix, app.Name, app.Id, usedNames);
            if (name.Length == 0)
            {
                continue;
            }

            var title = string.IsNullOrWhiteSpace(app.Name) ? "Agent 应用" : app.Name;
            var description = string.IsNullOrWhiteSpace(app.Description)
                ? "团队 Agent 应用工具：传入 query（要交给该 Agent 处理的完整问题或指令），可选在 history 携带近期对话上下文，返回该 Agent 的处理结果。"
                : $"{app.Description}（传入 query 交给该 Agent 处理并返回结果，可选携带 history 近期对话上下文。）";

            var appId = app.Id;
            tools.Add(new AppTool
            {
                Name = name,
                Title = title,
                Description = description,
                Kind = "agentapp",
                SourceId = appId,
                ParametersExample = "{\"query\":\"要交给该 Agent 处理的完整问题或指令\",\"history\":[{\"role\":\"user\",\"content\":\"先前的提问\"},{\"role\":\"assistant\",\"content\":\"先前的回答\"}]}",
                InvokeAsync = (argsJson, ct) => InvokeAsync(appId, context, argsJson, ct),
            });
        }

        return tools;
    }

    /// <summary>
    /// 无头单轮执行目标 Agent 应用：参数取 query（兼容别名键，复用流程工具的提取规则）与可选 history
    /// （[{role,content}]，截最近 N 条）；运行期循环防护命中、目标下线或执行异常统一转为工具错误文本，不中断对话.
    /// </summary>
    private async Task<AppToolResult> InvokeAsync(Guid agentAppId, AppAgentBuildContext context, string? argsJson, CancellationToken cancellationToken)
    {
        var query = WorkflowAppToolProvider.ExtractQuery(argsJson);
        if (string.IsNullOrWhiteSpace(query))
        {
            return AppToolResult.Fail("缺少 query 参数：请传入要交给该 Agent 处理的完整问题或指令文本.");
        }

        // 运行期循环防护（保存期闭包检测的兜底）：目标已在祖先调用链上说明形成环
        if (AppToolCallChain.Contains(agentAppId))
        {
            return AppToolResult.Fail("检测到 Agent 应用循环调用，已拒绝执行，请调整任务拆分或工具绑定.");
        }

        try
        {
            var app = await _databaseContext.Apps.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == agentAppId, cancellationToken).ConfigureAwait(false);
            if (app == null || app.IsDisable || app.AppType != (int)AppType.Agent || app.TeamId != context.TeamId || app.IsExternal || app.PublishStatus != 1)
            {
                return AppToolResult.Fail("绑定的 Agent 应用不存在、未发布或已下线.");
            }

            var config = await _databaseContext.AppAgentConfigs.AsNoTracking()
                .FirstOrDefaultAsync(x => x.AppId == agentAppId, cancellationToken).ConfigureAwait(false);
            if (config == null)
            {
                return AppToolResult.Fail("绑定的 Agent 应用未配置对话模型.");
            }

            // 正式调用按发布快照执行（与流程 agentApp 节点/流程工具绑定一致）
            var effective = AppAgentConfigSnapshot.ResolveEffectiveConfig(app, config, preferPublished: true);
            if (effective.ModelId == Guid.Empty)
            {
                return AppToolResult.Fail("绑定的 Agent 应用未配置对话模型.");
            }

            // 惰性解析（见类备注）：此时作用域内各服务已构造完成，不再进入构造递归
            var invoker = _serviceProvider.GetRequiredService<IWorkflowNodeAiInvoker>();

            // 调用方与目标都入链：目标再绑定其它 Agent 应用时可检测整条祖先路径
            using (AppToolCallChain.Push(context.AppId))
            using (AppToolCallChain.Push(agentAppId))
            {
                var reply = await invoker.AgentAppAsync(new WorkflowNodeAgentAppRequest
                {
                    App = app,
                    Config = effective,
                    UserId = context.UserId,
                    Prompt = query,
                    History = ExtractHistory(argsJson),
                }, cancellationToken).ConfigureAwait(false);

                return AppToolResult.Ok(reply);
            }
        }
#pragma warning disable CA1031 // 目标应用执行异常统一转为工具错误文本，避免中断对话
        catch (Exception ex)
        {
            return AppToolResult.Fail(ex.Message);
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// 从工具参数 JSON 提取可选 history：对象取 history 数组（元素 {role,content}，空内容跳过，
    /// 非 assistant 一律按 user），截最近 N 条；缺失或非法返回 null.
    /// </summary>
    private static List<ChatMessage>? ExtractHistory(string? argsJson)
    {
        if (string.IsNullOrWhiteSpace(argsJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(argsJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("history", out var history)
                || history.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var messages = new List<ChatMessage>();
            foreach (var item in history.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object
                    || !item.TryGetProperty("content", out var content)
                    || content.ValueKind != JsonValueKind.String
                    || string.IsNullOrWhiteSpace(content.GetString()))
                {
                    continue;
                }

                var role = item.TryGetProperty("role", out var roleElement)
                    && roleElement.ValueKind == JsonValueKind.String
                    && string.Equals(roleElement.GetString(), "assistant", StringComparison.OrdinalIgnoreCase)
                    ? ChatRole.Assistant
                    : ChatRole.User;
                messages.Add(new ChatMessage(role, content.GetString()!));
            }

            return messages.Count == 0 ? null : messages.TakeLast(MaxHistoryMessages).ToList();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

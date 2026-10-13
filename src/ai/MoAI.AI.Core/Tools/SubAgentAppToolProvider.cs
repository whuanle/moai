using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Maomi;
using Microsoft.Extensions.DependencyInjection;
using MoAI.AI.Models;
using MoAI.Infra.Exceptions;

namespace MoAI.AI.Services;

/// <summary>
/// 子代理工具来源：应用开启子代理协作时，为主代理提供 <c>spawn_subagent</c> 任务委派工具.
/// 子代理以本应用同配置在独立上下文中执行任务，仅结论回传；子代理装配（IsSubAgent）下不再注册（深度固定 1）.
/// </summary>
[InjectOnScoped]
public sealed class SubAgentAppToolProvider : IAppToolProvider
{
    /// <summary>
    /// 子代理委派工具名称.
    /// </summary>
    public const string ToolName = "spawn_subagent";

    // 禁止构造注入 ISubAgentInvoker（其依赖 AppContextProviderFactory → 全部工具提供者 → 本类，构成 DI 环），
    // 与 WorkflowAppToolProvider 同法：调用时惰性解析
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubAgentAppToolProvider"/> class.
    /// </summary>
    /// <param name="serviceProvider">服务提供者（惰性解析子代理执行端口）.</param>
    public SubAgentAppToolProvider(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    public int Order => 16;

    /// <inheritdoc/>
    public Task<IReadOnlyList<AppTool>> GetToolsAsync(AppAgentBuildContext context, CancellationToken cancellationToken)
    {
        // 子代理不再携带 spawn 工具（深度固定 1），未开启协作时同样不注册
        if (context.IsSubAgent || context.Config == null)
        {
            return Task.FromResult<IReadOnlyList<AppTool>>([]);
        }

        var settings = AppAgentExecutionSettings.Parse(context.Config.ExecutionSettings).SubAgent;
        if (settings is not { Enabled: true })
        {
            return Task.FromResult<IReadOnlyList<AppTool>>([]);
        }

        var timeoutSeconds = settings.TimeoutSeconds > 0 ? settings.TimeoutSeconds : SubAgentSettings.DefaultTimeoutSeconds;
        var resultMaxChars = settings.ResultMaxChars > 0 ? settings.ResultMaxChars : SubAgentSettings.DefaultResultMaxChars;
        var app = context.App;
        var config = context.Config;
        var userId = context.UserId;
        var sessionId = context.SessionId;

        var tool = new AppTool
        {
            Name = ToolName,
            Title = "子代理",
            Description = "创建一个子代理（与本应用同模型、同工具、同技能）在独立上下文中执行委派任务，仅最终结论回传当前对话。" +
                "适合把大段检索、批量阅读、多轮试错等高噪声工作隔离出去：用一句明确、可独立完成的任务描述委派；" +
                "复杂任务请拆成多次委派，不要在一个任务里塞多个目标。任务无法完成时会说明原因。",
            Kind = "subagent",
            ParametersExample = "{\"task\":\"要独立完成的任务描述\",\"guidance\":\"可选：执行要求或输出格式约定\"}",
            InvokeAsync = (argsJson, ct) => InvokeSubAgentAsync(app, config, userId, sessionId, timeoutSeconds, resultMaxChars, argsJson, ct),
        };

        return Task.FromResult<IReadOnlyList<AppTool>>([tool]);
    }

    private async Task<AppToolResult> InvokeSubAgentAsync(
        Database.Entities.AppEntity app,
        Database.Entities.AppAgentConfigEntity config,
        long userId,
        Guid sessionId,
        int timeoutSeconds,
        int resultMaxChars,
        string? argsJson,
        CancellationToken cancellationToken)
    {
        var (task, guidance) = ParseArgs(argsJson);
        if (string.IsNullOrWhiteSpace(task))
        {
            return AppToolResult.Fail("缺少参数 task（要委派给子代理的任务描述）.");
        }

        try
        {
            var invoker = _serviceProvider.GetRequiredService<ISubAgentInvoker>();
            var result = await invoker.InvokeAsync(new SubAgentInvocationRequest
            {
                App = app,
                Config = config,
                UserId = userId,
                ParentSessionId = sessionId,
                Task = task,
                Guidance = guidance,
                TimeoutSeconds = timeoutSeconds,
                ResultMaxChars = resultMaxChars,
            }, cancellationToken).ConfigureAwait(false);
            return AppToolResult.Ok(result);
        }
        catch (BusinessException ex)
        {
            return AppToolResult.Fail(ex.Message);
        }
    }

    private static (string? Task, string? Guidance) ParseArgs(string? argsJson)
    {
        if (string.IsNullOrWhiteSpace(argsJson) || argsJson.Trim() == "{}")
        {
            return (null, null);
        }

        try
        {
            using var document = JsonDocument.Parse(argsJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return (null, null);
            }

            string? task = null;
            string? guidance = null;
            if (document.RootElement.TryGetProperty("task", out var taskNode) && taskNode.ValueKind == JsonValueKind.String)
            {
                task = taskNode.GetString();
            }

            if (document.RootElement.TryGetProperty("guidance", out var guidanceNode) && guidanceNode.ValueKind == JsonValueKind.String)
            {
                guidance = guidanceNode.GetString();
            }

            return (task, guidance);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }
}

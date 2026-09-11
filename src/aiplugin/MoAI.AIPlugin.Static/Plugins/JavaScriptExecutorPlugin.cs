using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Jint;
using MoAI.AIPlugin.Attributes;
using MoAI.AIPlugin.Contracts;
using MoAI.AIPlugin.Static.Models;
using MoAI.Infra.Exceptions;

namespace MoAI.AIPlugin.Static.Plugins;

/// <summary>
/// JavaScript 执行器（静态插件）：使用 Jint 执行一段 JavaScript 代码，返回 run() 函数的结果.
/// </summary>
[AiPlugin(key: "static_javascript_executor", Name = "JavaScript 执行器", Description = "使用 Jint 执行 JavaScript 代码，限制最大内存 4MB、超时 4 秒、递归深度 100、最大语句 1000；要求代码定义一个无参 run() 函数并返回执行结果")]
public class JavaScriptExecutorPlugin : IStaticPluginRuntime<JavaScriptExecutorRequest, JavaScriptExecutorResponse>
{
    /// <summary>
    /// Jint 引擎内存上限（字节）.
    /// </summary>
    private const int MemoryLimitBytes = 4_000_000;

    /// <summary>
    /// Jint 引擎递归深度上限.
    /// </summary>
    private const int RecursionLimit = 100;

    /// <summary>
    /// Jint 引擎超时时间（秒）.
    /// </summary>
    private const int TimeoutSeconds = 4;

    /// <summary>
    /// Jint 引擎最大执行语句数.
    /// </summary>
    private const int MaxStatements = 1000;

    /// <inheritdoc/>
    [Description("JavaScript 代码示例：必须定义无参 run() 函数并把结果 return 出来")]
    public static string GetParamsExampleValue()
    {
        return @"{
  // 要执行的 JavaScript 代码，必须定义一个无参 run() 函数，把结果 return 出来
  ""Code"": ""function run() { return { id: 666, name: 'test' }; }""
}";
    }

    /// <inheritdoc/>
    public async Task<JavaScriptExecutorResponse> RunAsync(JavaScriptExecutorRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.Code))
        {
            throw new BusinessException(400, "JavaScript 代码不能为空");
        }

        object? result;
        try
        {
            // Jint 是 CPU 密集型同步执行，放入 Task.Run 以释放当前线程，
            // 并让 cancellation 能立即反映到 await 端（Jint 自身的超时/语句/内存限制仍负责实际中断）。
            result = await Task.Run(() => ExecuteJavaScript(request.Code), cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // 直接向上抛，让 PluginExecutor 归一化为取消结果。
            throw;
        }
        catch (BusinessException)
        {
            throw;
        }
#pragma warning disable CA1031 // 插件侧异常统一归一化为业务异常，错误消息透出 Jint 内部提示便于排查
        catch (Exception ex)
        {
#pragma warning restore CA1031
            throw new BusinessException(400, $"JavaScript 执行失败: {ex.Message}");
        }

        return new JavaScriptExecutorResponse { Result = result };
    }

    /// <summary>
    /// 在隔离的 Jint 引擎中执行代码并调用 run() 函数，返回 run() 的返回值（CLR 形态）.
    /// </summary>
    /// <param name="code">JavaScript 代码.</param>
    /// <returns>run() 的返回值；若未返回或返回 null/undefined 则返回 null.</returns>
    private static object? ExecuteJavaScript(string code)
    {
        using var engine = new Engine(options =>
        {
            options.LimitMemory(MemoryLimitBytes);
            options.LimitRecursion(RecursionLimit);
            options.TimeoutInterval(TimeSpan.FromSeconds(TimeoutSeconds));
            options.MaxStatements(MaxStatements);
        });

        var executed = engine.Execute(code);
        var runResult = executed.Invoke("run");

        if (runResult.IsNull() || runResult.IsUndefined())
        {
            return null;
        }

        return runResult.ToObject();
    }
}
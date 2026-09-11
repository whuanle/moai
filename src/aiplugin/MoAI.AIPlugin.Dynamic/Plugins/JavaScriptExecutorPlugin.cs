using System.Globalization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jint;
using Jint.Native;
using MoAI.AIPlugin.Attributes;
using MoAI.AIPlugin.Contracts;
using MoAI.AIPlugin.Dynamic.Models;
using MoAI.Infra.Exceptions;

namespace MoAI.AIPlugin.Dynamic.Plugins;

/// <summary>
/// JavaScript 执行器（动态插件）：执行实例配置中由用户填写的 JavaScript 代码，代码必须导出 <c>run(parameter)</c> 函数.
/// </summary>
/// <remarks>
/// 资源限制由 Jint 引擎在内层兜底（与旧版实现保持一致）：
/// <list type="bullet">
/// <item><description>最大内存 4 MB（<see cref="MaxMemoryBytes"/>）；</description></item>
/// <item><description>最大递归深度 100（<see cref="MaxRecursionDepth"/>）；</description></item>
/// <item><description>执行超时 4 s（<see cref="ExecutionTimeout"/>，由 Jint 按语句数与时间片轮询检查）；</description></item>
/// <item><description>最大执行语句 1000（<see cref="MaxStatements"/>，超出即抛 <c>StatementsCountOverflowException</c>）；</description></item>
/// </list>
/// 取消语义：每次执行重新 <c>new Engine</c>，在 <c>Task.Run</c> 内跑，外层用 <c>Task.WaitAsync</c> 让调用方取消可协作（被取消时 Jint 实例会被 GC 回收）。
/// 不在插件内 <c>new HttpClient</c>，无外部依赖；不持久化任何状态（每次运行独立实例）。
/// </remarks>
[AiPlugin(key: "javascript_executor", Name = "JavaScript 执行器", Description = "执行实例配置中的 JavaScript 代码（必须导出 run(parameter) 函数），限制最大内存 4MB、递归 100、超时 4s、最大语句 1000 行")]
public class JavaScriptExecutorPlugin : IDynamicPluginRuntime<JavaScriptExecutorRequest, JavaScriptExecutorResponse, JavaScriptExecutorConfig>
{
    /// <summary>Jint 引擎允许分配的最大堆内存（字节）.</summary>
    private const long MaxMemoryBytes = 4_000_000;

    /// <summary>JS 函数调用允许的最大递归深度.</summary>
    private const int MaxRecursionDepth = 100;

    /// <summary>单次执行允许的最大 JS 语句数.</summary>
    private const int MaxStatements = 1000;

    /// <summary>Jint 按语句数轮询检查的执行超时阈值.</summary>
    private static readonly TimeSpan ExecutionTimeout = TimeSpan.FromSeconds(4);

    private string _javaScriptCode = string.Empty;

    /// <inheritdoc/>
    public static string GetParamsExampleValue()
    {
        return """
            {
              "Parameters": "{\"id\":1,\"name\":\"test\"}" // 传给 run(parameter) 的字符串参数；通常填 JSON 文本
            }
            """;
    }

    /// <inheritdoc/>
    public static string GetConfigExampleValue()
    {
        return """
            {
              "JavaScriptCode": "// 必须定义 run(parameter)；parameter 是字符串\n// 返回值可以是对象、字符串、数字、布尔等任意类型\nfunction run(parameter) {\n    var obj = JSON.parse(parameter);\n    return {\n        id: obj.id,\n        name: 'test',\n        echoed: parameter\n    };\n}" // 要执行的 JavaScript 代码
            }
            """;
    }

    /// <inheritdoc/>
    public Task<string?> InitAsync(JavaScriptExecutorConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.JavaScriptCode))
        {
            return Task.FromResult<string?>("JavaScript 代码不能为空");
        }

        _javaScriptCode = config.JavaScriptCode;
        return Task.FromResult<string?>(null);
    }

    /// <inheritdoc/>
    public async Task<JavaScriptExecutorResponse> RunAsync(JavaScriptExecutorRequest request, CancellationToken cancellationToken)
    {
        var parameters = request.Parameters ?? string.Empty;

        // Jint 是同步阻塞模型：把执行放到 Task.Run 里，让外层 WaitAsync(ct) 让取消可协作；
        // 每次都 new Engine，超时/取消后由 GC 回收短命对象，避免泄漏。
        var result = await Task.Run(() => ExecuteScript(_javaScriptCode, parameters), cancellationToken)
            .ConfigureAwait(false);

        return new JavaScriptExecutorResponse
        {
            Parameters = parameters,
            ResultJson = result.ResultJson,
            ResultKind = result.ResultKind,
        };
    }

    /// <summary>
    /// 在独立的 Jint 引擎里执行用户脚本并调用约定的 <c>run(parameter)</c> 函数.
    /// </summary>
    /// <param name="code">用户在实例配置里填写的 JavaScript 代码.</param>
    /// <param name="parameters">透传给 <c>run</c> 的字符串参数.</param>
    /// <returns>返回值归一结果.</returns>
    /// <exception cref="BusinessException">脚本执行或函数调用失败时抛出（400 业务错误，错误信息含 Jint 内部异常说明），由 <c>PluginExecutor</c> 归一为运行结果失败.</exception>
    private static ScriptResult ExecuteScript(string code, string parameters)
    {
        using var engine = new Engine(options =>
        {
            options.LimitMemory(MaxMemoryBytes);
            options.LimitRecursion(MaxRecursionDepth);
            options.TimeoutInterval(ExecutionTimeout);
            options.MaxStatements(MaxStatements);
        });

        // 1. 把用户脚本载入作用域（应当定义 function run(parameter)）。
        //    Execute 自身不抛错时也会返回 last-expression 的值，此处丢弃。
        try
        {
            engine.Execute(code);
        }
#pragma warning disable CA1031 // 用户脚本异常需要把 Jint 报错统一翻译为业务错误再抛出
        catch (Exception ex)
#pragma warning restore CA1031
        {
            throw new BusinessException(400, $"JavaScript 代码执行失败: {ex.Message}") { StatusCode = 400 };
        }

        // 2. 取出 run 函数；必须是函数，否则提示用户补齐 run。
        //    必须先用 typeof 探测：脚本未定义 run 时直接 Evaluate("run") 会让 Jint 抛 ReferenceError，
        //    错误信息退化成不可读的 "run is not defined"，无法提示用户该怎么改。
        bool isRunFunction;
        try
        {
            isRunFunction = engine.Evaluate("typeof run === 'function'").AsBoolean();
        }
#pragma warning disable CA1031 // GetValue 偶发抛错（如脚本顶层抛错被延迟到此处），与下方一并归一
        catch (Exception ex)
#pragma warning restore CA1031
        {
            throw new BusinessException(400, $"JavaScript 代码执行失败: {ex.Message}") { StatusCode = 400 };
        }

        if (!isRunFunction)
        {
            throw new BusinessException(400, "JavaScript 代码必须定义 run(parameter) 函数") { StatusCode = 400 };
        }

        var runFunction = engine.Evaluate("run");

        // 3. 调用 run(parameter)；返回值按类型归一。
        JsValue jsResult;
        try
        {
            jsResult = engine.Invoke(runFunction, parameters);
        }
#pragma warning disable CA1031 // 业务错误归一化由 PluginExecutor 处理；这里只翻译为可读业务异常
        catch (Exception ex)
#pragma warning restore CA1031
        {
            throw new BusinessException(400, $"JavaScript 执行失败: {ex.Message}") { StatusCode = 400 };
        }

        return ToScriptResult(jsResult);
    }

    /// <summary>
    /// 把 Jint 返回的 <see cref="JsValue"/> 归一为可序列化结果.
    /// </summary>
    /// <param name="value">Jint 返回值.</param>
    /// <returns>含类型标记与 JSON 文本的结果.</returns>
    private static ScriptResult ToScriptResult(JsValue value)
    {
        // 顺序：null/undefined 必须先于 object 判断；array 也是 object，故 IsArray 先于 IsObject。
        if (value.IsNull())
        {
            return new ScriptResult(null, JsResultKind.Null);
        }

        if (value.IsUndefined())
        {
            return new ScriptResult(null, JsResultKind.Undefined);
        }

        if (value.IsString())
        {
            return new ScriptResult(JsonSerializer.Serialize(value.AsString()), JsResultKind.String);
        }

        if (value.IsBoolean())
        {
            return new ScriptResult(value.AsBoolean() ? "true" : "false", JsResultKind.Boolean);
        }

        if (value.IsNumber())
        {
            var number = value.AsNumber();

            // JS Number 是 double；整数落 double 时按 long 输出，否则用 round-trip 保留精度。
            string text = (double.IsFinite(number)
                          && Math.Floor(number) == number
                          && Math.Abs(number) < 1e15)
                ? number.ToString("0", CultureInfo.InvariantCulture)
                : number.ToString("R", CultureInfo.InvariantCulture);
            return new ScriptResult(text, JsResultKind.Number);
        }

        if (value.IsArray())
        {
            var dotnetArray = value.ToObject();
            return new ScriptResult(JsonSerializer.Serialize(dotnetArray), JsResultKind.Array);
        }

        if (value.IsObject())
        {
            var dotnetObject = value.ToObject();
            return new ScriptResult(JsonSerializer.Serialize(dotnetObject), JsResultKind.Object);
        }

        // 兜底：symbol/bigint 等暂不暴露给上游，转字符串返回。
        return new ScriptResult(JsonSerializer.Serialize(value.ToString()), JsResultKind.Object);
    }

    /// <summary>
    /// 脚本返回值归一结果.
    /// </summary>
    /// <param name="ResultJson">归一后的 JSON 文本（标量为字符串化的标量；对象/数组为 JSON 序列化文本）.</param>
    /// <param name="ResultKind">返回值类型标记（见 <c>JsResultKind</c>）.</param>
    private sealed record ScriptResult(string? ResultJson, string ResultKind);
}
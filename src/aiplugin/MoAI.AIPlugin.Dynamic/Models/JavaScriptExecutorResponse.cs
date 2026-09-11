using System.ComponentModel;
using System.Text.Json;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// JavaScript 执行器动态插件响应结果.
/// </summary>
public class JavaScriptExecutorResponse
{
    /// <summary>
    /// 实际传给 <c>run(parameter)</c> 的字符串参数.
    /// </summary>
    [Description("本次传给 run 的字符串参数")]
    public string Parameters { get; set; } = string.Empty;

    /// <summary>
    /// JS 函数返回值的归一化结果.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><see cref="JsResultKind.String"/>/<see cref="JsResultKind.Number"/>/<see cref="JsResultKind.Boolean"/>：<see cref="ResultJson"/> 为字符串化的标量.</description></item>
    /// <item><description><see cref="JsResultKind.Object"/>/<see cref="JsResultKind.Array"/>：<see cref="ResultJson"/> 为 JSON 序列化后的对象/数组.</description></item>
    /// <item><description><see cref="JsResultKind.Null"/>/<see cref="JsResultKind.Undefined"/>：<see cref="ResultJson"/> 为 null（视为脚本未返回有效结果）.</description></item>
    /// </list>
    /// </remarks>
    [Description("JS 函数返回值的归一化 JSON；与 ResultKind 配合解析")]
    public string? ResultJson { get; set; }

    /// <summary>
    /// 返回值类型，便于调用方按类型解析 <see cref="ResultJson"/>（取自 <see cref="JsResultKind"/>）.
    /// </summary>
    [Description("返回值类型：string/number/boolean/object/array/null/undefined")]
    public string ResultKind { get; set; } = string.Empty;

    /// <summary>
    /// 用 UTF-8 文本安全序列化任意标量值.
    /// </summary>
    /// <param name="value">要序列化的值.</param>
    /// <returns>序列化后的 JSON 文本.</returns>
    internal static string SerializeScalar(object? value) => value switch
    {
            null => "null",
            string s => JsonSerializer.Serialize(s),
            bool b => b ? "true" : "false",
            _ => JsonSerializer.Serialize(value),
    };
}
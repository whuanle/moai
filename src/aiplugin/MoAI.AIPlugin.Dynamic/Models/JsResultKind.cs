using System.Diagnostics.CodeAnalysis;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// JavaScript 函数返回值的归一化类型标记（与 <see cref="JavaScriptExecutorResponse.ResultKind"/> 配合使用）.
/// </summary>
/// <remarks>
/// 字段名沿用 ECMA 规范的 JS 类型名（<c>string</c>/<c>object</c> 等），与 <see cref="System.String"/>/<see cref="System.Object"/> 同名；
/// 实际取值仍是 JS 类型字符串，<see cref="SuppressMessageAttribute"/> 用于抑制 CA1720.
/// </remarks>
[SuppressMessage("Usage", "CA1720", Justification = "字段名沿用 ECMA JS 类型字面量（string/object/array/...），取值是 JS 类型字符串")]
public static class JsResultKind
{
    /// <summary>JS 字符串.</summary>
    public const string String = "string";

    /// <summary>JS 数字.</summary>
    public const string Number = "number";

    /// <summary>JS 布尔值.</summary>
    public const string Boolean = "boolean";

    /// <summary>JS 对象（不含数组）.</summary>
    public const string Object = "object";

    /// <summary>JS 数组.</summary>
    public const string Array = "array";

    /// <summary>JS null.</summary>
    public const string Null = "null";

    /// <summary>JS undefined.</summary>
    public const string Undefined = "undefined";
}
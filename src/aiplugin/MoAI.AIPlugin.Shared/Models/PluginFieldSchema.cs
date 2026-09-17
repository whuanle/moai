using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Text.Json.Serialization;

namespace MoAI.AIPlugin.Models;

/// <summary>
/// 插件字段元数据（请求参数或响应输出的单个字段），供设计器做智能提示与输出参数自动填充.
/// </summary>
public class PluginFieldSchema
{
    /// <summary>
    /// 字段 JSON 名称（优先取 <see cref="JsonPropertyNameAttribute"/>，否则 camelCase 属性名）.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 字段类型：string / number / boolean / array / object / dynamic.
    /// </summary>
    public string FieldType { get; set; } = "dynamic";

    /// <summary>
    /// 字段描述（取 <see cref="DescriptionAttribute"/>），可为空.
    /// </summary>
    public string? Description { get; set; }
}

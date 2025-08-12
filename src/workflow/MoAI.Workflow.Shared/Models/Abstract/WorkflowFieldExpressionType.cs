// <copyright file="Class1.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using System.Text.Json.Serialization;

namespace MoAI.Workflow.Models.Abact;

public enum WorkflowFieldExpressionType
{
    /// <summary>
    /// 使用变量赋值，支持系统变量、全局变量、节点变量等.
    /// </summary>
    [JsonPropertyName("variable")]
    Variable,

    /// <summary>
    /// JsonPath 表达式.
    /// </summary>
    [JsonPropertyName("jsonpath")]
    JsonPath,

    /// <summary>
    /// 字符串插值表达式，可使用变量插值.
    /// </summary>
    [JsonPropertyName("string_interpolation")]
    StringInterpolation,

    /// <summary>
    /// 正则表达式.
    /// </summary>
    [JsonPropertyName("regex")]
    Regex,
}

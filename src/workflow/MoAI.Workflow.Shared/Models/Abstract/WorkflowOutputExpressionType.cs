// <copyright file="Class1.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

namespace MoAI.Workflow.Models.Abact;

public enum WorkflowOutputExpressionType
{
    /// <summary>
      /// 无输出表达式.
      /// </summary>
    None = 0,

    /// <summary>
    /// 固定输出表达式，也就是每个字段可以使用 JsonPath 、字符串插值、正则表达式或者其它形式.
    /// </summary>
    Fixed = 1,

    /// <summary>
    /// 使用 JavaScript 脚本.
    /// </summary>
    JavaScript = 2
}

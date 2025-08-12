// <copyright file="Class1.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

namespace MoAI.Workflow.Models.Abact;

public class WorkflowOutputExpression
{
    /// <summary>
    /// 节点输出取值表达式类型.
    /// </summary>
    public WorkflowOutputExpressionType Type { get; init; } = WorkflowOutputExpressionType.None;

    /// <summary>
    /// 固定变量表达式.
    /// </summary>
    public IReadOnlyCollection<WorkflowInputExpression> Fixed { get; init; } = Array.Empty<WorkflowInputExpression>();

    /// <summary>
    /// 使用 JavaScript 脚本来生成输出.
    /// </summary>
    public string JavaScript { get; init; } = string.Empty;
}

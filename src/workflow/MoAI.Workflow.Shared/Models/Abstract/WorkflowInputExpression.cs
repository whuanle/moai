// <copyright file="WorkflowFieldOutputExpression.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

namespace MoAI.Workflow.Models.Abact;

public class WorkflowInputExpression
{
    /// <summary>
    /// 字段名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 取值表达式类型.
    /// </summary>
    public WorkflowFieldExpressionType ExpressionType { get; init; } = default!;

    /// <summary>
    /// 表达式的值.
    /// </summary>
    public string ExpressionValue { get; init; } = default!;
}

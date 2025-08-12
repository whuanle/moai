// <copyright file="Class1.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using System.Text.Json.Serialization;

namespace MoAI.Workflow.Models.Abact;

public class WorkflowEndNodeDefinition : WorkflowNodefinition
{
    /// <inheritdoc/>
    public override NodeType NodeType { get; protected init; } = NodeType.End;

    /// <summary>
    /// 输入参数.
    /// </summary>
    public IReadOnlyCollection<WorkflowField> Input { get; init; } = Array.Empty<WorkflowField>();

    /// <summary>
    /// 输出参数.
    /// </summary>
    public IReadOnlyCollection<WorkflowField> Output { get; init; } = Array.Empty<WorkflowField>();

    /// <summary>
    /// 生成输出的表达式.
    /// </summary>
    public WorkflowOutputExpression OutputExpression { get; init; } = new WorkflowOutputExpression();
}
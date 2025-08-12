// <copyright file="WorkflowNodefinition.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

namespace MoAI.Workflow.Models.Abact;

/// <summary>
/// 手动调用插件节点定义，固定一个输出变量 body.
/// </summary>
public class WorkflowPluginNodefinition : WorkflowNodefinition
{
    /// <summary>
    /// 插件id.
    /// </summary>
    public int PluginId { get; init; } = default!;

    /// <summary>
    /// 要调用的插件的 Action 名称.
    /// </summary>
    public string Action { get; init; } = default!;

    /// <inheritdoc/>
    public override NodeType NodeType { get; protected init; } = NodeType.Plugin;

    /// <summary>
    /// Header 参数.
    /// </summary>
    public IReadOnlyCollection<WorkflowField> Header { get; init; } = Array.Empty<WorkflowField>();

    /// <summary>
    /// 输入参数.
    /// </summary>
    public IReadOnlyCollection<WorkflowField> Input { get; init; } = Array.Empty<WorkflowField>();

    /// <summary>
    /// Header 取值的表达式.
    /// </summary>
    public WorkflowOutputExpression HeaderExpression { get; init; } = new WorkflowOutputExpression();

    /*
     固定输出变量 body，表示插件的返回值.
     */

    /// <summary>
    /// 输出参数.
    /// </summary>
    public IReadOnlyCollection<WorkflowField> Output { get; init; } = Array.Empty<WorkflowField>();

    /// <summary>
    /// 生成输出的表达式.
    /// </summary>
    public WorkflowOutputExpression OutputExpression { get; init; } = new WorkflowOutputExpression();
}

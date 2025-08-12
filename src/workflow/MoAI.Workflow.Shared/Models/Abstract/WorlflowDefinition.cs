// <copyright file="Class1.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

namespace MoAI.Workflow.Models.Abact;

/// <summary>
/// 流程定义.
/// </summary>
public class WorlflowDefinition
{
    /// <summary>
    /// guid.
    /// </summary>
    public Guid Id { get; init; } = Guid.CreateVersion7();

    /// <summary>
    /// 名字.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; init; } = default!;

    /// <summary>
    /// 起始节点.
    /// </summary>
    public WorkflowNodefinition StartNode { get; init; } = default!;

    /// <summary>
    /// 结束节点.
    /// </summary>
    public WorkflowNodefinition EndNode { get; init; } = default!;

    /// <summary>
    /// 节点集合.
    /// </summary>
    public IReadOnlyCollection<WorkflowNodefinition> Nodes { get; init; } = new List<WorkflowNodefinition>();

    /// <summary>
    /// 全局变量，启动该流程响应传递的变量.
    /// </summary>
    public IReadOnlyCollection<WorkflowField> Variables { get; init; } = new List<WorkflowField>();

    /// <summary>
    /// 固定全局系统变量.<br />
    /// sys.user_id<br />
    /// sys.app_id<br />
    /// sys.workflow_id<br />
    /// sys.workflow_run_id<br />
    /// </summary>
    public IReadOnlyCollection<WorkflowField> SystemVariables { get; init; } = new List<WorkflowField>();

    /// <summary>
    /// 连接集合，表示节点之间的相连.
    /// </summary>
    public IReadOnlyCollection<WorkflowConnection> Connections { get; init; } = new List<WorkflowConnection>();

    // todo:多轮对话历史记录
}

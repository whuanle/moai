// <copyright file="WorkflowNodefinition.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

namespace MoAI.Workflow.Models.Abact;

/// <summary>
/// 节点抽象.
/// </summary>
public abstract class WorkflowNodefinition
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
    /// 节点类型.
    /// </summary>
    public virtual NodeType NodeType { get; protected init; }
}

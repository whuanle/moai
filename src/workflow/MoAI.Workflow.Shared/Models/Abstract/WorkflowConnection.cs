// <copyright file="Class1.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

namespace MoAI.Workflow.Models.Abact;

public class WorkflowConnection
{
    /// <summary>
    /// guid.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// 起始节点.
    /// </summary>
    public Guid FromNodeId { get; init; }

    /// <summary>
    /// 结束节点.
    /// </summary>
    public Guid ToNodeId { get; init; }
}
// <copyright file="WorkflowField.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

namespace MoAI.Workflow.Models.Abact;

/// <summary>
/// 节点字段.
/// </summary>
public class WorkflowField
{
    /// <summary>
    /// 字段名称.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 字段类型.
    /// </summary>
    public WorkflowFieldType Type { get; set; }

    /// <summary>
    /// 默认值，以 json 字符串存储，使用是动态转换为对应的类型.
    /// </summary>
    public string Default { get; init; } = default!;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; init; } = default!;

    /// <summary>
    /// 排序.
    /// </summary>
    public int Index { get; init; }

    /// <summary>
    /// 是否必须.
    /// </summary>
    public bool IsRequired { get; init; }

    /// <summary>
    /// 子类型，如果读取字段是数组时需要填写，如果不是则不需要.
    /// </summary>
    public WorkflowFieldType ChildrenType { get; init; }

    /// <summary>
    /// 子层.
    /// </summary>
    public IReadOnlyCollection<WorkflowField> Children { get; init; } = new List<WorkflowField>();
}

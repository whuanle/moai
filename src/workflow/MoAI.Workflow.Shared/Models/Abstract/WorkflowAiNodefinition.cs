// <copyright file="WorkflowNodefinition.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

namespace MoAI.Workflow.Models.Abact;

/// <summary>
/// 大语言模型问答.
/// </summary>
public class WorkflowAiNodefinition : WorkflowNodefinition
{
    /// <summary>
    /// AI模型id.
    /// </summary>
    public int AiModelId { get; init; } = default!;

    /// <inheritdoc/>
    public override NodeType NodeType { get; protected init; } = NodeType.AiQuestion;

    /// <summary>
    /// 提示词.
    /// </summary>
    public string Prompt { get; init; } = default!;

    /// <summary>
    /// 用户问题.
    /// </summary>
    public WorkflowField Question { get; init; } = default!;

    /// <summary>
    /// 问题取值表达式.
    /// </summary>
    public WorkflowInputExpression QuestionExpression { get; init; } = new WorkflowInputExpression();

    /// <summary>
    /// 输出参数.
    /// </summary>
    public IReadOnlyCollection<WorkflowField> Output { get; init; } = Array.Empty<WorkflowField>();

    /// <summary>
    /// 生成输出的表达式.
    /// </summary>
    public WorkflowOutputExpression OutputExpression { get; init; } = new WorkflowOutputExpression();
}

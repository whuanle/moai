using System;
using System.Collections.Generic;

namespace MoAI.Wiki.Commands;

/// <summary>
/// 批量执行工作流的响应：逐文档执行结果.
/// </summary>
public class BatchRunWikiDocumentWorkflowCommandResponse
{
    /// <summary>
    /// 每个文档的处理结果，顺序与请求的文档 id 顺序一致.
    /// </summary>
    public List<BatchRunWikiDocumentWorkflowDocumentItem> Items { get; init; } = new();
}

/// <summary>
/// 批量执行工作流的单文档结果.
/// </summary>
public class BatchRunWikiDocumentWorkflowDocumentItem
{
    /// <summary>
    /// 文档 id.
    /// </summary>
    public long DocumentId { get; init; }

    /// <summary>
    /// 文档名称，文档不存在时可能为空.
    /// </summary>
    public string FileName { get; init; } = string.Empty;

    /// <summary>
    /// 本次是否成功（切割完成或任务已提交）.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// 结果说明（成功为已执行/已提交的步骤描述，失败为原因）.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// 异步任务 id；本文档提交了元数据生成/向量化任务时返回，纯切割时为空.
    /// </summary>
    public Guid? TaskId { get; init; }
}

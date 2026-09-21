using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.Wiki.Commands;

/// <summary>
/// 手动触发外部源同步：拉取外部文档并与上次内容比对，仅对发生变化的文档更新并按工作流处理.
/// 仅团队成员可操作.
/// </summary>
public class SyncWikiSourceCommand : IRequest<SyncWikiSourceCommandResponse>, IModelValidator<SyncWikiSourceCommand>, IUserIdContext
{
    /// <summary>
    /// 知识库 id，由 Controller 从路由参数回填.
    /// </summary>
    public long WikiId { get; init; }

    /// <summary>
    /// 外部源 id，由 Controller 从路由参数回填.
    /// </summary>
    public Guid SourceId { get; init; }

    /// <summary>
    /// 是否强制全量处理：为 true 时忽略内容哈希比对，对所有文档触发工作流.
    /// </summary>
    public bool Force { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<SyncWikiSourceCommand> validate)
    {
        // WikiId/SourceId 来自路由参数由 Controller 回填，自动验证发生在回填前，此处不做校验（否则恒 400）
    }
}

/// <summary>
/// 外部源同步结果.
/// </summary>
public class SyncWikiSourceCommandResponse
{
    /// <summary>
    /// 本次扫描到的外部文档总数.
    /// </summary>
    public int Total { get; init; }

    /// <summary>
    /// 新建的文档数.
    /// </summary>
    public int Created { get; init; }

    /// <summary>
    /// 内容有变化并已更新的文档数.
    /// </summary>
    public int Updated { get; init; }

    /// <summary>
    /// 内容无变化跳过的文档数.
    /// </summary>
    public int Unchanged { get; init; }

    /// <summary>
    /// 跳过的非文档类型节点数（如电子表格、多维表格）.
    /// </summary>
    public int Skipped { get; init; }

    /// <summary>
    /// 处理失败的文档数.
    /// </summary>
    public int Failed { get; init; }

    /// <summary>
    /// 已提交工作流的文档数.
    /// </summary>
    public int WorkflowTriggered { get; init; }

    /// <summary>
    /// 同步结果说明（失败时为错误原因）.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// 逐文档结果.
    /// </summary>
    public List<SyncWikiSourceDocumentResult> Items { get; init; } = new();
}

/// <summary>
/// 外部源同步的单个文档结果.
/// </summary>
public class SyncWikiSourceDocumentResult
{
    /// <summary>
    /// 外部文档标识（飞书为节点 token）.
    /// </summary>
    public string ExternalKey { get; init; } = string.Empty;

    /// <summary>
    /// 外部文档标题.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// 知识库文档 id，新建或更新成功后返回.
    /// </summary>
    public long? DocumentId { get; init; }

    /// <summary>
    /// 处理结果：created / updated / unchanged / skipped / failed.
    /// </summary>
    public string Result { get; init; } = string.Empty;

    /// <summary>
    /// 结果说明，失败时为错误原因.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// 触发的工作流任务 id，未触发时为空.
    /// </summary>
    public Guid? TaskId { get; init; }
}

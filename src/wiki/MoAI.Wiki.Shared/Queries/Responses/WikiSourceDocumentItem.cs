using System;
using MoAI.Wiki.Models;

namespace MoAI.Wiki.Queries.Responses;

/// <summary>
/// 外部源文档映射项：外部文档与知识库文档的对应关系与同步状态.
/// </summary>
public class WikiSourceDocumentItem
{
    /// <summary>
    /// 外部源 id.
    /// </summary>
    public Guid SourceId { get; init; }

    /// <summary>
    /// 外部文档唯一标识（飞书为节点 token）.
    /// </summary>
    public string ExternalKey { get; init; } = string.Empty;

    /// <summary>
    /// 外部文档标题.
    /// </summary>
    public string ExternalTitle { get; init; } = string.Empty;

    /// <summary>
    /// 外部文档在源中的路径.
    /// </summary>
    public string ExternalPath { get; init; } = string.Empty;

    /// <summary>
    /// 知识库文档 id.
    /// </summary>
    public long DocumentId { get; init; }

    /// <summary>
    /// 知识库文档名称.
    /// </summary>
    public string FileName { get; init; } = string.Empty;

    /// <summary>
    /// 同步状态.
    /// </summary>
    public WikiSourceDocumentStatus Status { get; init; }

    /// <summary>
    /// 最近一次同步时间.
    /// </summary>
    public DateTimeOffset? LastSyncTime { get; init; }

    /// <summary>
    /// 最近一次同步错误信息.
    /// </summary>
    public string LastError { get; init; } = string.Empty;

    /// <summary>
    /// 外部文档版本号（飞书为文档编辑时间）.
    /// </summary>
    public string Revision { get; init; } = string.Empty;
}

using System;
using System.Collections.Generic;
using MoAI.Database.Audits;

#pragma warning disable CA1051
#pragma warning disable SA1401
#pragma warning disable SA1600
#pragma warning disable SA1601
#pragma warning disable SA1204
namespace MoAI.Database.Entities;

/// <summary>
/// 外部源文档，外部源条目与知识库文档的映射，记录内容哈希用于增量比对.
/// </summary>
public partial class WikiSourceDocumentEntity : IFullAudited
{
    /// <summary>
    /// 自增主键.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 外部源id（wiki_source.id，逻辑关联，仓库约定不建物理外键）.
    /// </summary>
    public Guid SourceId { get; set; }

    /// <summary>
    /// 知识库id.
    /// </summary>
    public int WikiId { get; set; }

    /// <summary>
    /// 团队id.
    /// </summary>
    public int TeamId { get; set; }

    /// <summary>
    /// 知识库文档id（wiki_document.id，逻辑关联）.
    /// </summary>
    public int DocumentId { get; set; }

    /// <summary>
    /// 外部文档唯一标识，飞书文档源为节点token.
    /// </summary>
    public string ExternalKey { get; set; } = default!;

    /// <summary>
    /// 外部文档标题.
    /// </summary>
    public string ExternalTitle { get; set; } = default!;

    /// <summary>
    /// 外部文档在源中的路径（面包屑，斜杠分隔）.
    /// </summary>
    public string ExternalPath { get; set; } = default!;

    /// <summary>
    /// 最近一次同步内容的 SHA-256，用于判断是否变化.
    /// </summary>
    public string ContentHash { get; set; } = default!;

    /// <summary>
    /// 外部文档版本号（飞书为文档编辑时间/版本号），辅助判断变化.
    /// </summary>
    public string Revision { get; set; } = default!;

    /// <summary>
    /// 同步状态，见 WikiSourceDocumentStatus（0=待同步，1=已同步，2=失败）.
    /// </summary>
    public int Status { get; set; }

    /// <summary>
    /// 最近一次同步时间.
    /// </summary>
    public DateTimeOffset? LastSyncTime { get; set; }

    /// <summary>
    /// 最近一次同步错误信息.
    /// </summary>
    public string LastError { get; set; } = default!;

    /// <summary>
    /// 创建人.
    /// </summary>
    public long CreateUserId { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>
    /// 更新人.
    /// </summary>
    public long UpdateUserId { get; set; }

    /// <summary>
    /// 更新时间.
    /// </summary>
    public DateTimeOffset UpdateTime { get; set; }

    /// <summary>
    /// 软删除.
    /// </summary>
    public long IsDeleted { get; set; }

    /// <summary>
    /// 外部文档内容标识，飞书文档源为文档token（obj_token），云文档变更事件按此匹配.
    /// </summary>
    public string ExternalDocToken { get; set; } = default!;
}

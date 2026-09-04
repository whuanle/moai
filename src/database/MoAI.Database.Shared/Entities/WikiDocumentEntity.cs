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
/// 知识库文档，挂在知识库下的内容页.
/// </summary>
public partial class WikiDocumentEntity : IFullAudited
{
    /// <summary>
    /// 文档ID，自增主键.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 所属知识库ID，逻辑关联wiki.id（仓库约定不建物理外键）.
    /// </summary>
    public long WikiId { get; set; }

    /// <summary>
    /// 文档标题，最长100字符.
    /// </summary>
    public string Title { get; set; } = default!;

    /// <summary>
    /// 文档内容（Markdown，text 不限长）.
    /// </summary>
    public string Content { get; set; } = default!;

    /// <summary>
    /// 软删除.
    /// </summary>
    public long IsDeleted { get; set; }

    /// <summary>
    /// 创建人用户ID，审计钩子插入时自动填充.
    /// </summary>
    public long CreateUserId { get; set; }

    /// <summary>
    /// 创建时间，审计钩子自动填充，默认timezone(utc,now()).
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>
    /// 最后修改人用户ID，审计钩子更新/删除时自动填充.
    /// </summary>
    public long UpdateUserId { get; set; }

    /// <summary>
    /// 更新时间，审计钩子插入/更新/删除时自动刷新.
    /// </summary>
    public DateTimeOffset UpdateTime { get; set; }
}

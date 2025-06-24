using System;
using System.Collections.Generic;
using MoAI.Database.Audits;

namespace MoAI.Database.Entities;

/// <summary>
/// 知识库任务.
/// </summary>
public partial class WikiDocumntTaskEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 知识库id.
    /// </summary>
    public int WikiId { get; set; }

    /// <summary>
    /// 文档id.
    /// </summary>
    public int DocumentId { get; set; }

    /// <summary>
    /// 任务标识.
    /// </summary>
    public string TaskTag { get; set; } = default!;

    /// <summary>
    /// 任务状态.
    /// </summary>
    public int State { get; set; }

    /// <summary>
    /// 消息.
    /// </summary>
    public string Message { get; set; } = default!;

    /// <summary>
    /// 每段最大token数量.
    /// </summary>
    public int MaxTokensPerParagraph { get; set; }

    /// <summary>
    /// 重叠的token数量.
    /// </summary>
    public int OverlappingTokens { get; set; }

    /// <summary>
    /// 分词器.
    /// </summary>
    public string Tokenizer { get; set; } = default!;

    /// <summary>
    /// 创建人.
    /// </summary>
    public int CreateUserId { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>
    /// 最后修改人.
    /// </summary>
    public int UpdateUserId { get; set; }

    /// <summary>
    /// 最后更新时间.
    /// </summary>
    public DateTimeOffset UpdateTime { get; set; }

    /// <summary>
    /// 软删除.
    /// </summary>
    public long IsDeleted { get; set; }
}

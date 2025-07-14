using System;
using System.Collections.Generic;
using MoAI.Database.Audits;

namespace MoAI.Database.Entities;

/// <summary>
/// 对话历史.
/// </summary>
public partial class ChatHistoryEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 对话id.
    /// </summary>
    public long ChatId { get; set; }

    /// <summary>
    /// 话题标题.
    /// </summary>
    public string Title { get; set; } = default!;

    /// <summary>
    /// 对话属性.
    /// </summary>
    public string ExecutionSettings { get; set; } = default!;

    /// <summary>
    /// 模型id.
    /// </summary>
    public int ModelId { get; set; }

    /// <summary>
    /// 使用的知识库id.
    /// </summary>
    public int WikiId { get; set; }

    /// <summary>
    /// 插件列表.
    /// </summary>
    public string PluginIds { get; set; } = default!;

    /// <summary>
    /// 创建人.
    /// </summary>
    public int CreateUserId { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>
    /// 更新人.
    /// </summary>
    public int UpdateUserId { get; set; }

    /// <summary>
    /// 更新时间.
    /// </summary>
    public DateTimeOffset UpdateTime { get; set; }

    /// <summary>
    /// 软删除.
    /// </summary>
    public long IsDeleted { get; set; }
}

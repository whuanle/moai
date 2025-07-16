﻿using System;
using System.Collections.Generic;
using MoAI.Database.Audits;

namespace MoAI.Database.Entities;

/// <summary>
/// 对话历史，不保存实际历史记录.
/// </summary>
public partial class AppAssistantChatHistoryEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 对话id.
    /// </summary>
    public Guid ChatId { get; set; } = default!;

    /// <summary>
    /// 对话id.
    /// </summary>
    public string CompletionsId { get; set; } = default!;

    /// <summary>
    /// 内容.
    /// </summary>
    public string Content { get; set; } = default!;

    /// <summary>
    /// 角色.
    /// </summary>
    public string Role { get; set; } = default!;

    /// <summary>
    /// 模型名称.
    /// </summary>
    public string Model { get; set; } = default!;

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

using System;
using System.Collections.Generic;
using MoAI.Database.Audits;

namespace MoAI.Database.Entities;

/// <summary>
/// ai助手表.
/// </summary>
public partial class AppAssistantChatEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 对话标题.
    /// </summary>
    public string Title { get; set; } = default!;

    /// <summary>
    /// 提示词.
    /// </summary>
    public string Prompt { get; set; } = default!;

    /// <summary>
    /// 对话使用的模型 id.
    /// </summary>
    public int ModelId { get; set; }

    /// <summary>
    /// 要使用的知识库id.
    /// </summary>
    public int WikiId { get; set; }

    /// <summary>
    /// 要使用的插件id.
    /// </summary>
    public string PluginIds { get; set; } = default!;

    /// <summary>
    /// 对话影响参数.
    /// </summary>
    public string ExecutionSettings { get; set; } = default!;

    /// <summary>
    /// 输入token统计.
    /// </summary>
    public int InputTokens { get; set; }

    /// <summary>
    /// 输出token统计.
    /// </summary>
    public int OutTokens { get; set; }

    /// <summary>
    /// 使用的 token 总数.
    /// </summary>
    public int TotalTokens { get; set; }

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

using System;
using System.Collections.Generic;
using MoAI.Database.Audits;

namespace MoAI.Database.Entities;

/// <summary>
/// 普通应用.
/// </summary>
public partial class AppCommonEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 团队id.
    /// </summary>
    public int TeamId { get; set; }

    /// <summary>
    /// 所属应用id.
    /// </summary>
    public Guid AppId { get; set; }

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
    public string WikiIds { get; set; } = default!;

    /// <summary>
    /// 要使用的插件.
    /// </summary>
    public string Plugins { get; set; } = default!;

    /// <summary>
    /// 对话影响参数.
    /// </summary>
    public string ExecutionSettings { get; set; } = default!;

    /// <summary>
    /// AI头像,存 objectKey.
    /// </summary>
    public string Avatar { get; set; } = default!;

    /// <summary>
    /// 是否开启授权验证.
    /// </summary>
    public bool IsAuth { get; set; }

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

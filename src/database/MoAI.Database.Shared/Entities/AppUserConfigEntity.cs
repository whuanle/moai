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
/// 用户级应用配置，(app_id, user_id) 唯一，跨会话复用.
/// </summary>
public partial class AppUserConfigEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 应用 id，逻辑关联 app.id.
    /// </summary>
    public Guid AppId { get; set; }

    /// <summary>
    /// 用户 id，逻辑关联 user.id.
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// 所属团队 id，逻辑关联 app.team_id，冗余用于团队维度过滤.
    /// </summary>
    public int TeamId { get; set; }

    /// <summary>
    /// 用户为新会话选择的专家提示词 id，0=未设置.
    /// </summary>
    public int PromptId { get; set; }

    /// <summary>
    /// 用户勾选启用的技能 id 列表 JSON 数组，须为应用默认技能（app_agent_config.skills）的子集.
    /// </summary>
    public string Skills { get; set; } = default!;

    public long CreateUserId { get; set; }

    public DateTimeOffset CreateTime { get; set; }

    public long UpdateUserId { get; set; }

    public DateTimeOffset UpdateTime { get; set; }

    public long IsDeleted { get; set; }

    /// <summary>
    /// 工具审批模式：auto=自动执行；approval=重要工具调用前需人工批准.
    /// </summary>
    public string ToolApprovalMode { get; set; } = default!;
}

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
/// 技能.
/// </summary>
public partial class SkillEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 技能标识，全局唯一，蛇形命名，创建后不可变更.
    /// </summary>
    public string Key { get; set; } = default!;

    /// <summary>
    /// 技能名称.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 技能描述，作为 Agent 工具列表中的能力说明.
    /// </summary>
    public string Description { get; set; } = default!;

    /// <summary>
    /// 使用说明（markdown），技能加载时注入给 Agent.
    /// </summary>
    public string Instructions { get; set; } = default!;

    /// <summary>
    /// 技能包文件清单 JSON.
    /// </summary>
    public string Files { get; set; } = default!;

    /// <summary>
    /// 是否系统内置技能：脚本以程序集内嵌资源分发，不可删除.
    /// </summary>
    public bool IsSystem { get; set; }

    /// <summary>
    /// 所属团队 id，&gt;0=团队技能，0=系统级技能（is_system）或个人技能（归属 create_user_id）.
    /// </summary>
    public int TeamId { get; set; }

    /// <summary>
    /// 是否禁用.
    /// </summary>
    public bool IsDisable { get; set; }

    public long CreateUserId { get; set; }

    public DateTimeOffset CreateTime { get; set; }

    public long UpdateUserId { get; set; }

    public DateTimeOffset UpdateTime { get; set; }

    public long IsDeleted { get; set; }

    /// <summary>
    /// 是否公开（市场上架审批通过后置为 true），公开技能全员可见可用.
    /// </summary>
    public bool IsPublic { get; set; }
}

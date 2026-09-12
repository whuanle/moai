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
/// <para>
/// 系统级能力包：说明（Instructions）+ 脚本/资源文件（Files JSON），
/// 应用挂载后由 Agent 在会话沙箱中按需加载执行.
/// </para>
/// </summary>
public partial class SkillEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 技能标识，全局唯一，蛇形命名（字母开头，仅字母/数字/下划线），创建后不可变更.
    /// </summary>
    public string Key { get; set; } = default!;

    /// <summary>
    /// 技能名称.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 技能描述，作为 Agent 工具列表中的能力说明.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 使用说明（markdown），技能加载时注入给 Agent.
    /// </summary>
    public string Instructions { get; set; } = string.Empty;

    /// <summary>
    /// 技能包文件清单 JSON：[{"path":"scripts/xx.py","fileId":1,"fileName":"xx.py"}].
    /// </summary>
    public string Files { get; set; } = "[]";

    /// <summary>
    /// 是否系统内置技能：内置技能的脚本以程序集内嵌资源分发，不可删除.
    /// </summary>
    public bool IsSystem { get; set; }

    /// <summary>
    /// 所属团队 id，0=系统级技能.
    /// </summary>
    public int TeamId { get; set; }

    /// <summary>
    /// 是否禁用.
    /// </summary>
    public bool IsDisable { get; set; }

    /// <summary>
    /// 创建人.
    /// </summary>
    public long CreateUserId { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>
    /// 最后修改人.
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
}

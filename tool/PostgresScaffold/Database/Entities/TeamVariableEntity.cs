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
/// 团队变量，插件配置以 ${key} 引用.
/// </summary>
public partial class TeamVariableEntity : IFullAudited
{
    /// <summary>
    /// 变量ID，自增主键.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 所属团队ID，逻辑关联team.id（仓库约定不建物理外键）.
    /// </summary>
    public long TeamId { get; set; }

    /// <summary>
    /// 变量名称.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 变量名，团队内唯一（字母开头，字母/数字/下划线）.
    /// </summary>
    public string Key { get; set; } = default!;

    /// <summary>
    /// 变量值；普通变量明文，私密变量 AES 密文.
    /// </summary>
    public string Value { get; set; } = default!;

    /// <summary>
    /// 是否私密变量：true 值仅管理员可见.
    /// </summary>
    public bool IsSecret { get; set; }

    /// <summary>
    /// 变量描述，最长255字符，空串=未填写.
    /// </summary>
    public string Description { get; set; } = default!;

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

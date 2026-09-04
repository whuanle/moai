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
/// 团队，知识库/插件等资源的管理单元.
/// </summary>
public partial class TeamEntity : IFullAudited
{
    /// <summary>
    /// 团队ID，自增主键.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 团队名称，最长50字符.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 团队简介，最长255字符，空串=未填写.
    /// </summary>
    public string Description { get; set; } = default!;

    /// <summary>
    /// 团队头像的存储ObjectKey（桶内路径），非完整URL，展示时经GetPublicFileUrl转公开地址，空串=未设置.
    /// </summary>
    public string AvatarPath { get; set; } = default!;

    /// <summary>
    /// 是否禁用团队：true=团队及下级资源停用，由管理员操作，不影响成员账号登录.
    /// </summary>
    public bool IsDisable { get; set; }

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

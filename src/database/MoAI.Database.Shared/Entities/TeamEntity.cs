using System;
using MoAI.Database.Audits;

#pragma warning disable CA1051
#pragma warning disable SA1401
#pragma warning disable SA1600
#pragma warning disable SA1601
#pragma warning disable SA1204
namespace MoAI.Database.Entities;

/// <summary>
/// 团队，知识库、插件等资源的管理单元，资源通过 team_id 挂载到团队.
/// </summary>
public partial class TeamEntity : IFullAudited
{
    /// <summary>
    /// 团队ID，自增主键（identity）.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 团队名称，最长 50 字符；partial 唯一索引 (name) WHERE is_deleted = false 保证未删除范围内全局唯一，
    /// 已删除行不参与唯一约束，同名团队可反复"删除后重建"且历史全保留.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 团队简介，最长 255 字符，默认空串表示未填写.
    /// </summary>
    public string Description { get; set; } = default!;

    /// <summary>
    /// 团队头像的存储 ObjectKey（对象存储桶内路径），不是完整 URL；
    /// 展示时通过存储服务的 GetPublicFileUrl 转为 /static 公开地址（用法参考 QueryUserListCommandHandler 对用户头像的处理），默认空串表示未设置头像.
    /// </summary>
    public string AvatarPath { get; set; } = default!;

    /// <summary>
    /// 是否禁用团队：true 时团队及其下资源停用，由管理员在管理端操作；不影响成员本人的账号登录.
    /// </summary>
    public bool IsDisable { get; set; }

    /// <summary>
    /// 软删除标记：false=未删除；true=已删除.
    /// <para>
    /// 删除时由 DatabaseContext 审计钩子通过 <see cref="IDeleteAudited"/> 接口写入（接口 setter 将非零 ticks 转为 true），
    /// 查询过滤 is_deleted == false 由框架自动追加，业务代码均直接使用本 bool 属性.
    /// </para>
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// 创建人用户ID，插入时由 DatabaseContext 审计钩子自动填充为当前登录用户，业务代码无需赋值.
    /// </summary>
    public long CreateUserId { get; set; }

    /// <summary>
    /// 创建时间，插入时由审计钩子自动填充；数据库默认值 timezone('utc'::text, now()).
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>
    /// 最后修改人用户ID，更新/删除时由审计钩子自动填充为当前登录用户.
    /// </summary>
    public long UpdateUserId { get; set; }

    /// <summary>
    /// 更新时间，插入/更新/删除时由审计钩子自动刷新.
    /// </summary>
    public DateTimeOffset UpdateTime { get; set; }

    /// <summary>
    /// 审计接口适配：仓库共享软删除框架按 long ticks（<see cref="DateTimeOffset.Now.Ticks"/>）读写，
    /// 本实体对业务暴露 bool，通过显式接口实现做 0/1 转换，兼容审计钩子而不影响现有表.
    /// </summary>
    long IDeleteAudited.IsDeleted
    {
        get => IsDeleted ? 1 : 0;
        set => IsDeleted = value != 0;
    }
}

using System;
using MoAI.Database.Audits;
using MoAI.Database.Enums;

#pragma warning disable CA1051
#pragma warning disable SA1401
#pragma warning disable SA1600
#pragma warning disable SA1601
#pragma warning disable SA1204
namespace MoAI.Database.Entities;

/// <summary>
/// 团队成员，用户与团队的多对多关联；
/// partial 唯一索引 (team_id, user_id) WHERE is_deleted = false 保证同一用户在同一团队只有一条有效记录，
/// 移出团队（软删除）后可重新加入，且移除历史全保留.
/// </summary>
public partial class TeamUserEntity : IFullAudited
{
    /// <summary>
    /// 自增主键（identity）.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 所属团队ID，逻辑关联 team.id；按仓库约定不建物理外键，由应用层保证一致性.
    /// </summary>
    public long TeamId { get; set; }

    /// <summary>
    /// 成员用户ID，逻辑关联 user.id；按仓库约定不建物理外键，由应用层保证一致性.
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// 成员角色，见 <see cref="TeamRole"/>：
    /// Owner(0)=团队所有者，可解散团队、转让所有权、管理一切；
    /// Admin(1)=管理员，可管理成员并创建知识库/插件等团队资源；
    /// Member(2)=普通成员，只读使用；新增成员默认 Member.
    /// </summary>
    public TeamRole Role { get; set; }

    /// <summary>
    /// 软删除标记：false=在团队中；true=已移出.
    /// <para>
    /// 移除时由 DatabaseContext 审计钩子通过 <see cref="IDeleteAudited"/> 接口写入（接口 setter 将非零 ticks 转为 true），
    /// 查询过滤 is_deleted == false 由框架自动追加，业务代码均直接使用本 bool 属性.
    /// </para>
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// 邀请人/添加人用户ID，插入时由 DatabaseContext 审计钩子自动填充为当前登录用户.
    /// </summary>
    public long CreateUserId { get; set; }

    /// <summary>
    /// 加入时间，插入时由审计钩子自动填充；数据库默认值 timezone('utc'::text, now())（UTC）.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>
    /// 最后修改人用户ID，角色变更/移除时由审计钩子自动填充为当前登录用户.
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

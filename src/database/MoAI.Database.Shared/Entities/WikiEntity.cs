using System;
using MoAI.Database.Audits;

#pragma warning disable CA1051
#pragma warning disable SA1401
#pragma warning disable SA1600
#pragma warning disable SA1601
#pragma warning disable SA1204
namespace MoAI.Database.Entities;

/// <summary>
/// 知识库，挂在团队下的资源，后续文档/集合等经 wiki_id 关联.
/// </summary>
public partial class WikiEntity : IFullAudited
{
    /// <summary>
    /// 知识库ID，自增主键（identity）.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 所属团队ID，逻辑关联 team.id（仓库约定不建物理外键）.
    /// </summary>
    public long TeamId { get; set; }

    /// <summary>
    /// 知识库名称，最长 50 字符；partial 唯一索引 (team_id, name) WHERE is_deleted = false 保证同一团队内未删除范围唯一，删除后同名可重建.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 知识库简介，最长 255 字符，默认空串表示未填写.
    /// </summary>
    public string Description { get; set; } = default!;

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

using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoAI.Database.Entities;

#pragma warning disable CA1051
#pragma warning disable SA1401
#pragma warning disable SA1600
#pragma warning disable SA1601
#pragma warning disable SA1204
namespace MoAI.Database;

/// <summary>
/// 组织角色：全局层级角色树节点.
/// </summary>
internal partial class OrgRoleConfiguration : IEntityTypeConfiguration<OrgRoleEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<OrgRoleEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("idx_org_role_primary");

        entity.ToTable("org_role", tb => tb.HasComment("组织角色：全局层级角色树（与团队无关），path 物化路径前缀匹配即得子树"));

        // 物化路径在未删除记录中唯一（id 链天然唯一，索引兜底）
        entity.HasIndex(e => e.Path)
            .IsUnique()
            .HasDatabaseName("idx_org_role_path_uindex")
            .HasFilter("\"is_deleted\" = 0");

        entity.HasIndex(e => e.ParentId, "idx_org_role_parent_id");

        entity.Property(e => e.Id)
            .HasComment("自增主键")
            .HasColumnName("id");
        entity.Property(e => e.Name)
            .HasMaxLength(50)
            .HasComment("角色名称，同一父角色下唯一")
            .HasColumnName("name");
        entity.Property(e => e.ParentId)
            .HasComment("上级角色id，null=根角色")
            .HasColumnName("parent_id");
        entity.Property(e => e.Path)
            .HasMaxLength(255)
            .HasDefaultValueSql("''::text")
            .HasComment("物化路径：根为 /{id}/，子级逐段追加（如 /1/3/8/）")
            .HasColumnName("path");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("创建时间，审计钩子自动填充")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("创建人，审计钩子自动填充")
            .HasColumnName("create_user_id");
        entity.Property(e => e.IsDeleted)
            .HasDefaultValueSql("'0'::bigint")
            .HasComment("软删除")
            .HasColumnName("is_deleted");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("更新时间，审计钩子自动填充")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasComment("最后修改人，审计钩子自动填充")
            .HasColumnName("update_user_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<OrgRoleEntity> modelBuilder);
}

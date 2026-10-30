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
/// 组织角色成员绑定，用户与组织角色多对多关联.
/// </summary>
internal partial class OrgUserRoleConfiguration : IEntityTypeConfiguration<OrgUserRoleEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<OrgUserRoleEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("idx_org_user_role_primary");

        entity.ToTable("org_user_role", tb => tb.HasComment("组织角色成员绑定：用户持有哪些组织角色，多对多"));

        // 同一用户同一角色只保留一条有效绑定
        entity.HasIndex(e => new { e.UserId, e.RoleId }, "idx_org_user_role_user_role_live_uindex")
            .IsUnique()
            .HasFilter("(is_deleted = 0)");

        entity.HasIndex(e => e.RoleId, "idx_org_user_role_role_id");

        entity.Property(e => e.Id)
            .HasComment("自增主键")
            .HasColumnName("id");
        entity.Property(e => e.UserId)
            .HasComment("用户id，逻辑关联user.id（仓库约定不建物理外键）")
            .HasColumnName("user_id");
        entity.Property(e => e.RoleId)
            .HasComment("组织角色id，逻辑关联org_role.id")
            .HasColumnName("role_id");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("绑定时间，审计钩子自动填充")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("分配人，审计钩子自动填充")
            .HasColumnName("create_user_id");
        entity.Property(e => e.IsDeleted)
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

    partial void OnConfigurePartial(EntityTypeBuilder<OrgUserRoleEntity> modelBuilder);
}

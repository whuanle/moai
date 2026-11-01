using System;
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
/// 统一组织授权，跨资源类型共用，一条记录只挂一个组织.
/// </summary>
internal partial class OrgGrantConfiguration : IEntityTypeConfiguration<OrgGrantEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<OrgGrantEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("idx_org_grant_primary");

        entity.ToTable("org_grant", tb => tb.HasComment("统一组织授权：资源按组织线定向放通，一条记录只挂一个组织"));

        // 同一资源同一角色只保留一条有效授权，重复授权即改写方向
        entity.HasIndex(e => new { e.ResourceType, e.ResourceId, e.OrgRoleId }, "idx_org_grant_resource_role_live_uindex")
            .IsUnique()
            .HasFilter("(is_deleted = 0)");

        entity.HasIndex(e => e.OrgRoleId, "idx_org_grant_role_id");

        entity.Property(e => e.Id)
            .HasComment("自增主键")
            .HasColumnName("id");
        entity.Property(e => e.ResourceType)
            .HasComment("资源类型（OrgGrantResourceType 枚举值）")
            .HasColumnName("resource_type");
        entity.Property(e => e.ResourceId)
            .HasComment("资源主键的字符串形态（应用为 Guid、知识库等为 long，统一存字符串）")
            .HasMaxLength(64)
            .HasColumnName("resource_id");
        entity.Property(e => e.OrgRoleId)
            .HasComment("组织id，逻辑关联org_role.id")
            .HasColumnName("org_role_id");
        entity.Property(e => e.Scope)
            .HasComment("授权方向（OrgGrantScope 枚举值：self 仅该角色 / up 含祖先 / down 含子树）")
            .HasColumnName("scope");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("授权时间，审计钩子自动填充")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("授权人，审计钩子自动填充")
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

    partial void OnConfigurePartial(EntityTypeBuilder<OrgGrantEntity> modelBuilder);
}

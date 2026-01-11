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
/// 系统接入.
/// </summary>
public partial class ExternalAppConfiguration : IEntityTypeConfiguration<ExternalAppEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ExternalAppEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("PRIMARY");

        entity.ToTable("external_app", tb => tb.HasComment("系统接入"));

        entity.Property(e => e.Id)
            .HasDefaultValueSql("unhex(replace(uuid(),'-',''))")
            .HasComment("app_id")
            .HasColumnName("id");
        entity.Property(e => e.Avatar)
            .HasMaxLength(255)
            .HasDefaultValueSql("''")
            .HasComment("头像objectKey")
            .HasColumnName("avatar");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("current_timestamp()")
            .HasComment("创建时间")
            .HasColumnType("datetime")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("创建人")
            .HasColumnType("int(11)")
            .HasColumnName("create_user_id");
        entity.Property(e => e.Description)
            .HasMaxLength(255)
            .HasComment("描述")
            .HasColumnName("description");
        entity.Property(e => e.IsDeleted)
            .HasComment("软删除")
            .HasColumnType("bigint(20)")
            .HasColumnName("is_deleted");
        entity.Property(e => e.IsDsiable)
            .HasComment("禁用")
            .HasColumnName("is_dsiable");
        entity.Property(e => e.Key)
            .HasMaxLength(255)
            .HasComment("应用密钥")
            .HasColumnName("key");
        entity.Property(e => e.Name)
            .HasMaxLength(20)
            .HasComment("应用名称")
            .HasColumnName("name");
        entity.Property(e => e.TeamId)
            .HasComment("团队id")
            .HasColumnType("int(11)")
            .HasColumnName("team_id");
        entity.Property(e => e.UpdateTime)
            .ValueGeneratedOnAddOrUpdate()
            .HasDefaultValueSql("current_timestamp()")
            .HasComment("更新时间")
            .HasColumnType("datetime")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasComment("更新人")
            .HasColumnType("int(11)")
            .HasColumnName("update_user_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<ExternalAppEntity> modelBuilder);
}

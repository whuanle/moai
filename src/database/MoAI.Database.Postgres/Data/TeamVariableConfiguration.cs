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
/// 团队变量.
/// </summary>
internal partial class TeamVariableConfiguration : IEntityTypeConfiguration<TeamVariableEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<TeamVariableEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("team_variable_pkey");

        entity.ToTable("team_variable", tb => tb.HasComment("团队变量"));

        entity.HasIndex(e => e.TeamId, "idx_team_variable_team_id");

        entity.HasIndex(e => new { e.TeamId, e.Key }, "idx_team_variable_team_key_live_uindex")
            .IsUnique()
            .HasFilter("is_deleted = false");

        entity.Property(e => e.Id)
            .HasComment("变量ID")
            .HasColumnName("id");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("创建时间")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("创建人")
            .HasColumnName("create_user_id");
        entity.Property(e => e.Description)
            .HasMaxLength(255)
            .HasDefaultValueSql("''::character varying")
            .HasComment("变量描述")
            .HasColumnName("description");
        entity.Property(e => e.GroupName)
            .HasMaxLength(50)
            .HasDefaultValueSql("''::character varying")
            .HasComment("分组名，仅组织用途")
            .HasColumnName("group_name");
        entity.Property(e => e.IsDeleted)
            .HasComment("软删除：false=未删除，true=已删除；审计钩子经接口适配写入")
            .HasColumnName("is_deleted");
        entity.Property(e => e.IsSecret)
            .HasComment("是否私密变量：true 值仅管理员可见且 AES 加密落库")
            .HasColumnName("is_secret");
        entity.Property(e => e.Key)
            .HasMaxLength(100)
            .HasComment("变量名，团队内唯一")
            .HasColumnName("key");
        entity.Property(e => e.TeamId)
            .HasComment("所属团队ID")
            .HasColumnName("team_id");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("更新时间")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasComment("最后修改人")
            .HasColumnName("update_user_id");
        entity.Property(e => e.Value)
            .HasComment("变量值；普通变量明文，私密变量 AES 密文")
            .HasColumnName("value");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<TeamVariableEntity> modelBuilder);
}

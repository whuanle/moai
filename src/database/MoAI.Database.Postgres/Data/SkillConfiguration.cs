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
/// 技能.
/// </summary>
internal partial class SkillConfiguration : IEntityTypeConfiguration<SkillEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<SkillEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("skill_pkey");

        entity.ToTable("skill", tb => tb.HasComment("技能"));

        entity.HasIndex(e => e.Key, "idx_skill_key_live_uindex")
            .IsUnique()
            .HasFilter("(is_deleted = 0)");

        entity.HasIndex(e => e.TeamId, "idx_skill_team_id");

        entity.Property(e => e.Id)
            .HasDefaultValueSql("uuid_generate_v4()")
            .HasComment("id")
            .HasColumnName("id");
        entity.Property(e => e.ClassifyId)
            .HasComment("分类id")
            .HasColumnName("classify_id");
        entity.Property(e => e.AvatarPath)
            .HasMaxLength(255)
            .HasDefaultValueSql("''::character varying")
            .HasComment("技能头像 objectKey，空串=未设置")
            .HasColumnName("avatar_path");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId).HasColumnName("create_user_id");
        entity.Property(e => e.Description)
            .HasMaxLength(255)
            .HasDefaultValueSql("''::character varying")
            .HasComment("技能描述，作为 Agent 工具列表中的能力说明")
            .HasColumnName("description");
        entity.Property(e => e.Files)
            .HasDefaultValueSql("'[]'::text")
            .HasComment("技能包文件清单 JSON")
            .HasColumnName("files");
        entity.Property(e => e.Instructions)
            .HasDefaultValueSql("''::text")
            .HasComment("使用说明（markdown），技能加载时注入给 Agent")
            .HasColumnName("instructions");
        entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
        entity.Property(e => e.IsDisable)
            .HasComment("是否禁用")
            .HasColumnName("is_disable");
        entity.Property(e => e.IsPublic)
            .HasComment("是否公开（市场上架审批通过后置为 true），公开技能全员可见可用")
            .HasColumnName("is_public");
        entity.Property(e => e.IsSystem)
            .HasComment("是否系统内置技能：脚本以程序集内嵌资源分发，不可删除")
            .HasColumnName("is_system");
        entity.Property(e => e.Key)
            .HasMaxLength(30)
            .HasComment("技能标识，全局唯一，蛇形命名，创建后不可变更")
            .HasColumnName("key");
        entity.Property(e => e.Name)
            .HasMaxLength(50)
            .HasComment("技能名称")
            .HasColumnName("name");
        entity.Property(e => e.TeamId)
            .HasComment("所属团队 id，>0=团队技能，0=系统级技能（is_system）或个人技能（归属 create_user_id）")
            .HasColumnName("team_id");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId).HasColumnName("update_user_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<SkillEntity> modelBuilder);
}

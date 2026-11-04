using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoAI.Database.Enums;
using MoAI.Database.Entities;

#pragma warning disable CA1051
#pragma warning disable SA1401
#pragma warning disable SA1600
#pragma warning disable SA1601
#pragma warning disable SA1204
namespace MoAI.Database;

/// <summary>
/// 团队资源 key.
/// </summary>
internal partial class TeamResourceKeyConfiguration : IEntityTypeConfiguration<TeamResourceKeyEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<TeamResourceKeyEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("idx_teamres_key_primary");

        entity.ToTable("teamres_key", tb => tb.HasComment("团队资源 key（应用 ACP/A2A、知识库、知识图谱外部访问凭证）"));

        entity.Property(e => e.Id)
            .HasDefaultValueSql("uuid_generate_v4()")
            .HasComment("id")
            .HasColumnName("id");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .HasComment("创建时间")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("创建人")
            .HasColumnName("create_user_id");
        entity.Property(e => e.IsDeleted)
            .HasDefaultValueSql("'0'::bigint")
            .HasComment("软删除")
            .HasColumnName("is_deleted");
        entity.Property(e => e.Key)
            .HasMaxLength(255)
            .HasComment("key 原文（moai-tr- 前缀，明文存储等值命中）")
            .HasColumnName("key");
        entity.Property(e => e.KeyPrefix)
            .HasMaxLength(16)
            .HasComment("key 展示前缀")
            .HasColumnName("key_prefix");
        entity.Property(e => e.ResourceId)
            .HasMaxLength(64)
            .HasComment("资源 id（字符串形态）：app 行=应用 Guid、wiki 行=wikiId、knowledge_graph 行=kgId；一个资源一把 key")
            .HasColumnName("resource_id");
        entity.Property(e => e.ResourceType)
            .HasDefaultValue(TeamResourceType.App)
            .HasComment("资源类型：1=app 团队应用（ACP/A2A）2=wiki 知识库 3=knowledge_graph 知识图谱")
            .HasColumnName("resource_type");
        entity.Property(e => e.Status)
            .HasDefaultValue(TeamResourceKeyStatus.Enabled)
            .HasComment("状态：0=enabled 1=disabled")
            .HasColumnName("status");
        entity.Property(e => e.TeamId)
            .HasComment("归属团队 id")
            .HasColumnName("team_id");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("更新时间")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasComment("更新人")
            .HasColumnName("update_user_id");

        OnConfigurePartial(entity);

        entity.HasIndex(e => new { e.Key, e.IsDeleted }, "idx_teamres_key_key_uindex").IsUnique();
        entity.HasIndex(e => e.TeamId, "idx_teamres_key_team_id");
        entity.HasIndex(e => new { e.TeamId, e.ResourceType, e.ResourceId, e.IsDeleted }, "idx_teamres_key_resource");
    }

    partial void OnConfigurePartial(EntityTypeBuilder<TeamResourceKeyEntity> modelBuilder);
}

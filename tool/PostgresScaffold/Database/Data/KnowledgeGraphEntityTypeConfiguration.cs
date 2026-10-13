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

internal partial class KnowledgeGraphEntityTypeConfiguration : IEntityTypeConfiguration<KnowledgeGraphEntityTypeEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<KnowledgeGraphEntityTypeEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("knowledge_graph_entity_type_pkey");

        entity.ToTable("knowledge_graph_entity_type");

        entity.HasIndex(e => e.KnowledgeGraphId, "idx_knowledge_graph_entity_type_knowledge_graph_id");

        entity.HasIndex(e => new { e.KnowledgeGraphId, e.Name }, "idx_knowledge_graph_entity_type_name_live_uindex")
            .IsUnique()
            .HasFilter("(is_deleted = 0)");

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.Color)
            .HasMaxLength(20)
            .HasDefaultValueSql("''::character varying")
            .HasColumnName("color");
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
            .HasColumnName("description");
        entity.Property(e => e.IsDeleted)
            .HasComment("软删除，0=未删除")
            .HasColumnName("is_deleted");
        entity.Property(e => e.KnowledgeGraphId).HasColumnName("knowledge_graph_id");
        entity.Property(e => e.Name)
            .HasMaxLength(50)
            .HasColumnName("name");
        entity.Property(e => e.Properties)
            .HasDefaultValueSql("'[]'::jsonb")
            .HasComment("属性定义 JSON")
            .HasColumnType("jsonb")
            .HasColumnName("properties");
        entity.Property(e => e.Sort).HasColumnName("sort");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("更新时间")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasComment("更新人")
            .HasColumnName("update_user_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<KnowledgeGraphEntityTypeEntity> modelBuilder);
}

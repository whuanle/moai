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

internal partial class KnowledgeGraphRelationTypeConfiguration : IEntityTypeConfiguration<KnowledgeGraphRelationTypeEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<KnowledgeGraphRelationTypeEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("knowledge_graph_relation_type_pkey");

        entity.ToTable("knowledge_graph_relation_type");

        entity.HasIndex(e => e.KnowledgeGraphId, "idx_knowledge_graph_relation_type_knowledge_graph_id");

        entity.HasIndex(e => new { e.KnowledgeGraphId, e.Name }, "idx_knowledge_graph_relation_type_name_live_uindex")
            .IsUnique()
            .HasFilter("(is_deleted = 0)");

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.Color)
            .HasMaxLength(20)
            .HasDefaultValueSql("''::character varying")
            .HasColumnName("color");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId).HasColumnName("create_user_id");
        entity.Property(e => e.Description)
            .HasMaxLength(255)
            .HasDefaultValueSql("''::character varying")
            .HasColumnName("description");
        entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
        entity.Property(e => e.KnowledgeGraphId).HasColumnName("knowledge_graph_id");
        entity.Property(e => e.Name)
            .HasMaxLength(50)
            .HasColumnName("name");
        entity.Property(e => e.Sort).HasColumnName("sort");
        entity.Property(e => e.SourceTypeId).HasColumnName("source_type_id");
        entity.Property(e => e.TargetTypeId).HasColumnName("target_type_id");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId).HasColumnName("update_user_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<KnowledgeGraphRelationTypeEntity> modelBuilder);
}

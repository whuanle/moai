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

internal partial class KnowledgeGraphConfiguration : IEntityTypeConfiguration<KnowledgeGraphEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<KnowledgeGraphEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("knowledge_graph_pkey");

        entity.ToTable("knowledge_graph");

        entity.HasIndex(e => e.TeamId, "idx_knowledge_graph_team_id");

        entity.HasIndex(e => e.Name, "idx_knowledge_graph_name_live_uindex")
            .IsUnique()
            .HasFilter("(is_deleted = 0)");

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId).HasColumnName("create_user_id");
        entity.Property(e => e.Database)
            .HasMaxLength(100)
            .HasColumnName("database");
        entity.Property(e => e.Description)
            .HasMaxLength(255)
            .HasDefaultValueSql("''::character varying")
            .HasColumnName("description");
        entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
        entity.Property(e => e.Mode)
            .HasMaxLength(20)
            .HasDefaultValueSql("'managed'::character varying")
            .HasColumnName("mode");
        entity.Property(e => e.Name)
            .HasMaxLength(50)
            .HasColumnName("name");
        entity.Property(e => e.TeamId).HasColumnName("team_id");
        entity.Property(e => e.TemplateKey)
            .HasMaxLength(50)
            .HasColumnName("template_key");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId).HasColumnName("update_user_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<KnowledgeGraphEntity> modelBuilder);
}

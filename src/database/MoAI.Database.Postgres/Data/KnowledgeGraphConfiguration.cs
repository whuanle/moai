using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoAI.Database.Entities;

namespace MoAI.Database;

/// <summary>
/// 知识图谱.
/// </summary>
internal partial class KnowledgeGraphConfiguration : IEntityTypeConfiguration<KnowledgeGraphEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<KnowledgeGraphEntity> builder)
    {
        builder.HasKey(e => e.Id).HasName("idx_kg_primary");
        builder.ToTable("kg", tb => tb.HasComment("知识图谱"));

        builder.Property(e => e.Id).HasComment("id").HasColumnName("id");
        builder.Property(e => e.TeamId).HasComment("所属团队 id").HasColumnName("team_id");
        builder.Property(e => e.Name).HasMaxLength(50).HasComment("名称").HasColumnName("name");
        builder.Property(e => e.Description).HasMaxLength(255).HasDefaultValueSql("''::character varying").HasComment("简介").HasColumnName("description");
        builder.Property(e => e.TemplateKey).HasMaxLength(50).HasComment("模板 key").HasColumnName("template_key");
        builder.Property(e => e.Mode).HasMaxLength(20).HasDefaultValueSql("'managed'::character varying").HasComment("来源").HasColumnName("mode");
        builder.Property(e => e.Database).HasMaxLength(100).HasComment("接入数据库名").HasColumnName("database");
        builder.Property(e => e.CreateTime).HasDefaultValueSql("timezone('utc'::text, now())").HasComment("创建时间").HasColumnName("create_time");
        builder.Property(e => e.CreateUserId).HasComment("创建人").HasColumnName("create_user_id");
        builder.Property(e => e.UpdateTime).HasDefaultValueSql("timezone('utc'::text, now())").HasComment("更新时间").HasColumnName("update_time");
        builder.Property(e => e.UpdateUserId).HasComment("最后修改人").HasColumnName("update_user_id");
        builder.Property(e => e.IsDeleted).HasDefaultValueSql("'0'::bigint").HasComment("软删除").HasColumnName("is_deleted");

        builder.HasIndex(e => e.TeamId).HasDatabaseName("idx_kg_team_id");
        builder.HasIndex(e => new { e.TeamId, e.Name }).IsUnique().HasFilter("is_deleted = 0").HasDatabaseName("idx_kg_team_name_live_uindex");
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoAI.Database.Entities;

namespace MoAI.Database;

/// <summary>
/// 知识图谱关系类型.
/// </summary>
internal partial class KnowledgeGraphRelationTypeConfiguration : IEntityTypeConfiguration<KnowledgeGraphRelationTypeEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<KnowledgeGraphRelationTypeEntity> builder)
    {
        builder.HasKey(e => e.Id).HasName("idx_kg_relation_type_primary");
        builder.ToTable("kg_relation_type", tb => tb.HasComment("知识图谱关系类型"));

        builder.Property(e => e.Id).HasComment("id").HasColumnName("id");
        builder.Property(e => e.KgId).HasComment("所属图谱 id").HasColumnName("kg_id");
        builder.Property(e => e.Name).HasMaxLength(50).HasComment("关系类型名称").HasColumnName("name");
        builder.Property(e => e.Color).HasMaxLength(20).HasDefaultValueSql("''::character varying").HasComment("颜色").HasColumnName("color");
        builder.Property(e => e.Description).HasMaxLength(255).HasDefaultValueSql("''::character varying").HasComment("描述").HasColumnName("description");
        builder.Property(e => e.SourceTypeId).HasComment("起点类型 id").HasColumnName("source_type_id");
        builder.Property(e => e.TargetTypeId).HasComment("终点类型 id").HasColumnName("target_type_id");
        builder.Property(e => e.Sort).HasComment("排序").HasColumnName("sort");
        builder.Property(e => e.CreateTime).HasDefaultValueSql("timezone('utc'::text, now())").HasComment("创建时间").HasColumnName("create_time");
        builder.Property(e => e.CreateUserId).HasComment("创建人").HasColumnName("create_user_id");
        builder.Property(e => e.UpdateTime).HasDefaultValueSql("timezone('utc'::text, now())").HasComment("更新时间").HasColumnName("update_time");
        builder.Property(e => e.UpdateUserId).HasComment("最后修改人").HasColumnName("update_user_id");
        builder.Property(e => e.IsDeleted).HasDefaultValueSql("'0'::bigint").HasComment("软删除").HasColumnName("is_deleted");
    }
}

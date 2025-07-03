using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoAI.Database.Entities;

namespace MoAI.Database;

/// <summary>
/// 知识库.
/// </summary>
public partial class WikiConfiguration : IEntityTypeConfiguration<WikiEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<WikiEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("PRIMARY");

        entity.ToTable("wiki", tb => tb.HasComment("知识库"));

        entity.Property(e => e.Id)
            .HasComment("id")
            .HasColumnType("int(11)")
            .HasColumnName("id");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("utc_timestamp()")
            .HasComment("创建时间")
            .HasColumnType("datetime")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("创建人")
            .HasColumnType("int(11)")
            .HasColumnName("create_user_id");
        entity.Property(e => e.Description)
            .HasMaxLength(255)
            .HasComment("知识库描述")
            .HasColumnName("description");
        entity.Property(e => e.IsDeleted)
            .HasComment("软删除")
            .HasColumnType("bigint(20)")
            .HasColumnName("is_deleted");
        entity.Property(e => e.Name)
            .HasMaxLength(20)
            .HasComment("知识库名称")
            .HasColumnName("name");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("utc_timestamp()")
            .HasComment("最后更新时间")
            .HasColumnType("datetime")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasComment("最后修改人")
            .HasColumnType("int(11)")
            .HasColumnName("update_user_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<WikiEntity> modelBuilder);
}

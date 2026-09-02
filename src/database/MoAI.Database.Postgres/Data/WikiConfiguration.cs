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
/// 知识库.
/// </summary>
internal partial class WikiConfiguration : IEntityTypeConfiguration<WikiEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<WikiEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("wiki_pkey");

        entity.ToTable("wiki", tb => tb.HasComment("知识库"));

        entity.HasIndex(e => e.TeamId, "idx_wiki_team_id");

        entity.HasIndex(e => new { e.TeamId, e.Name }, "idx_wiki_team_name_live_uindex")
            .IsUnique()
            .HasFilter("is_deleted = false");

        entity.Property(e => e.Id)
            .HasComment("知识库ID")
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
            .HasComment("知识库简介")
            .HasColumnName("description");
        entity.Property(e => e.IsDeleted)
            .HasComment("软删除：false=未删除，true=已删除；审计钩子经接口适配写入")
            .HasColumnName("is_deleted");
        entity.Property(e => e.Name)
            .HasMaxLength(50)
            .HasComment("知识库名称")
            .HasColumnName("name");
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

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<WikiEntity> modelBuilder);
}

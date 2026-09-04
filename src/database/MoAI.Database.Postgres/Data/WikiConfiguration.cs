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
/// 知识库，挂在团队下的资源.
/// </summary>
internal partial class WikiConfiguration : IEntityTypeConfiguration<WikiEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<WikiEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("wiki_pkey");

        entity.ToTable("wiki", tb => tb.HasComment("知识库，挂在团队下的资源"));

        entity.HasIndex(e => e.TeamId, "idx_wiki_team_id");

        entity.HasIndex(e => new { e.TeamId, e.Name }, "idx_wiki_team_name_live_uindex")
            .IsUnique()
            .HasFilter("(is_deleted = 0)");

        entity.Property(e => e.Id)
            .HasComment("知识库ID，自增主键")
            .HasColumnName("id");
        entity.Property(e => e.AvatarPath)
            .HasMaxLength(255)
            .HasComment("知识库头像路径，最长255字符")
            .HasColumnName("avatar_path");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("创建时间，审计钩子自动填充，默认timezone(utc,now())")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("创建人用户ID，审计钩子插入时自动填充")
            .HasColumnName("create_user_id");
        entity.Property(e => e.Description)
            .HasMaxLength(255)
            .HasDefaultValueSql("''::character varying")
            .HasComment("知识库简介，最长255字符，空串=未填写")
            .HasColumnName("description");
        entity.Property(e => e.IsDeleted)
            .HasComment("软删除")
            .HasColumnName("is_deleted");
        entity.Property(e => e.Name)
            .HasMaxLength(50)
            .HasComment("知识库名称，最长50字符")
            .HasColumnName("name");
        entity.Property(e => e.TeamId)
            .HasComment("所属团队ID，逻辑关联team.id（仓库约定不建物理外键）")
            .HasColumnName("team_id");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("更新时间，审计钩子插入/更新/删除时自动刷新")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasComment("最后修改人用户ID，审计钩子更新/删除时自动填充")
            .HasColumnName("update_user_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<WikiEntity> modelBuilder);
}

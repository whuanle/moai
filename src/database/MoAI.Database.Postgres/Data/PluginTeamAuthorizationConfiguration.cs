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
/// 授权私有系统插件给哪些团队使用.
/// </summary>
internal partial class PluginTeamAuthorizationConfiguration : IEntityTypeConfiguration<PluginTeamAuthorizationEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<PluginTeamAuthorizationEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("plugin_team_authorization_pkey");

        entity.ToTable("plugin_team_authorization", tb => tb.HasComment("授权私有系统插件给哪些团队使用"));

        entity.HasIndex(e => e.PluginId, "idx_plugin_team_authorization_plugin_id");

        entity.HasIndex(e => new { e.PluginId, e.TeamId }, "idx_plugin_team_authorization_plugin_team_uindex")
            .IsUnique()
            .HasFilter("(is_deleted = 0)");

        entity.HasIndex(e => e.TeamId, "idx_plugin_team_authorization_team_id");

        entity.Property(e => e.Id)
            .HasComment("自增主键")
            .HasColumnName("id");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("创建时间，审计钩子自动填充，默认timezone(utc,now())")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("创建人用户ID，审计钩子插入时自动填充")
            .HasColumnName("create_user_id");
        entity.Property(e => e.IsDeleted)
            .HasComment("软删除：0=未删除，非0=已删除（审计钩子自动写入）")
            .HasColumnName("is_deleted");
        entity.Property(e => e.PluginId)
            .HasComment("系统插件记录 id（plugin.id，逻辑关联，仓库约定不建物理外键）")
            .HasColumnName("plugin_id");
        entity.Property(e => e.TeamId)
            .HasComment("授权团队id，该团队成员可以使用该私有插件（逻辑关联 team.id）")
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

    partial void OnConfigurePartial(EntityTypeBuilder<PluginTeamAuthorizationEntity> modelBuilder);
}

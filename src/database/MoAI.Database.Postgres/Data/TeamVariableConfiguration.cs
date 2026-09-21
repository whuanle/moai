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
/// 团队变量，插件配置以 ${key} 引用.
/// </summary>
internal partial class TeamVariableConfiguration : IEntityTypeConfiguration<TeamVariableEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<TeamVariableEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("team_variable_pkey");

        entity.ToTable("team_variable", tb => tb.HasComment("团队变量，插件配置以 ${key} 引用"));

        entity.HasIndex(e => e.TeamId, "idx_team_variable_team_id");

        entity.HasIndex(e => new { e.TeamId, e.Key }, "idx_team_variable_team_key_live_uindex")
            .IsUnique()
            .HasFilter("(is_deleted = 0)");

        entity.Property(e => e.Id)
            .HasComment("变量ID，自增主键")
            .HasColumnName("id");
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
            .HasComment("变量描述，最长255字符，空串=未填写")
            .HasColumnName("description");
        entity.Property(e => e.IsDeleted)
            .HasComment("软删除")
            .HasColumnName("is_deleted");
        entity.Property(e => e.IsSecret)
            .HasComment("是否私密变量：true 值仅管理员可见")
            .HasColumnName("is_secret");
        entity.Property(e => e.Key)
            .HasMaxLength(100)
            .HasComment("变量名，团队内唯一（字母开头，字母/数字/下划线）")
            .HasColumnName("key");
        entity.Property(e => e.Name)
            .HasMaxLength(50)
            .HasDefaultValueSql("''::character varying")
            .HasComment("变量名称")
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
        entity.Property(e => e.Value)
            .HasDefaultValueSql("''::text")
            .HasComment("变量值；普通变量明文，私密变量 AES 密文")
            .HasColumnName("value");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<TeamVariableEntity> modelBuilder);
}

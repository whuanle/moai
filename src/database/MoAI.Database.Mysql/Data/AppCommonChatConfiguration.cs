using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoAI.Database.Entities;

namespace MoAI.Database;

/// <summary>
/// 普通应用对话表.
/// </summary>
public partial class AppCommonChatConfiguration : IEntityTypeConfiguration<AppCommonChatEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AppCommonChatEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("PRIMARY");

        entity.ToTable("app_common_chat", tb => tb.HasComment("普通应用对话表"));

        entity.Property(e => e.Id)
            .HasDefaultValueSql("unhex(replace(uuid(),'-',''))")
            .HasComment("id")
            .HasColumnName("id");
        entity.Property(e => e.AppId)
            .HasComment("appid")
            .HasColumnName("app_id");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("current_timestamp()")
            .HasComment("创建时间")
            .HasColumnType("datetime")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("创建人")
            .HasColumnType("int(11)")
            .HasColumnName("create_user_id");
        entity.Property(e => e.InputTokens)
            .HasComment("输入token统计")
            .HasColumnType("int(11)")
            .HasColumnName("input_tokens");
        entity.Property(e => e.IsDeleted)
            .HasComment("软删除")
            .HasColumnType("bigint(20)")
            .HasColumnName("is_deleted");
        entity.Property(e => e.OutTokens)
            .HasComment("输出token统计")
            .HasColumnType("int(11)")
            .HasColumnName("out_tokens");
        entity.Property(e => e.TeamId)
            .HasComment("团队id")
            .HasColumnType("int(11)")
            .HasColumnName("team_id");
        entity.Property(e => e.Title)
            .HasMaxLength(100)
            .HasDefaultValueSql("'未命名标题'")
            .HasComment("对话标题")
            .HasColumnName("title");
        entity.Property(e => e.TotalTokens)
            .HasComment("使用的 token 总数")
            .HasColumnType("int(11)")
            .HasColumnName("total_tokens");
        entity.Property(e => e.UpdateTime)
            .ValueGeneratedOnAddOrUpdate()
            .HasDefaultValueSql("current_timestamp()")
            .HasComment("更新时间")
            .HasColumnType("datetime")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasComment("更新人")
            .HasColumnType("int(11)")
            .HasColumnName("update_user_id");
        entity.Property(e => e.UserType)
            .HasComment("用户类型")
            .HasColumnType("int(11)")
            .HasColumnName("user_type");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<AppCommonChatEntity> modelBuilder);
}

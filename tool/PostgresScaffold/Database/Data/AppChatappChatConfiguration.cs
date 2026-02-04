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
/// 普通应用对话表.
/// </summary>
internal partial class AppChatappChatConfiguration : IEntityTypeConfiguration<AppChatappChatEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AppChatappChatEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("idx_65673_primary");

        entity.ToTable("app_chatapp_chat", tb => tb.HasComment("普通应用对话表"));

        entity.Property(e => e.Id)
            .HasDefaultValueSql("uuid_generate_v4()")
            .HasComment("id")
            .HasColumnName("id");
        entity.Property(e => e.AppId)
            .HasDefaultValueSql("uuid_generate_v4()")
            .HasComment("appid")
            .HasColumnName("app_id");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .HasComment("创建时间")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasDefaultValue(0)
            .HasComment("创建人")
            .HasColumnName("create_user_id");
        entity.Property(e => e.InputTokens)
            .HasDefaultValue(0)
            .HasComment("输入token统计")
            .HasColumnName("input_tokens");
        entity.Property(e => e.IsDeleted)
            .HasDefaultValueSql("'0'::bigint")
            .HasComment("软删除")
            .HasColumnName("is_deleted");
        entity.Property(e => e.OutTokens)
            .HasDefaultValue(0)
            .HasComment("输出token统计")
            .HasColumnName("out_tokens");
        entity.Property(e => e.Title)
            .HasMaxLength(100)
            .HasDefaultValueSql("'未命名标题'::character varying")
            .HasComment("对话标题")
            .HasColumnName("title");
        entity.Property(e => e.TotalTokens)
            .HasDefaultValue(0)
            .HasComment("使用的 token 总数")
            .HasColumnName("total_tokens");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("更新时间")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasDefaultValue(0)
            .HasComment("更新人")
            .HasColumnName("update_user_id");
        entity.Property(e => e.UserType)
            .HasDefaultValue(0)
            .HasComment("用户类型")
            .HasColumnName("user_type");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<AppChatappChatEntity> modelBuilder);
}

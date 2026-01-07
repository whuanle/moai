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
/// 对话历史，不保存实际历史记录.
/// </summary>
public partial class AppCommonChatHistoryConfiguration : IEntityTypeConfiguration<AppCommonChatHistoryEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AppCommonChatHistoryEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("PRIMARY");

        entity.ToTable("app_common_chat_history", tb => tb.HasComment("对话历史，不保存实际历史记录"));

        entity.Property(e => e.Id).HasComment("id");
        entity.Property(e => e.ChatId).HasComment("对话id");
        entity.Property(e => e.CompletionsId).HasComment("对话id");
        entity.Property(e => e.Content).HasComment("内容");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("current_timestamp()")
            .HasComment("创建时间");
        entity.Property(e => e.CreateUserId).HasComment("创建人");
        entity.Property(e => e.IsDeleted).HasComment("软删除");
        entity.Property(e => e.Role).HasComment("角色");
        entity.Property(e => e.TeamId).HasComment("团队id");
        entity.Property(e => e.UpdateTime)
            .ValueGeneratedOnAddOrUpdate()
            .HasDefaultValueSql("current_timestamp()")
            .HasComment("更新时间");
        entity.Property(e => e.UpdateUserId).HasComment("更新人");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<AppCommonChatHistoryEntity> modelBuilder);
}

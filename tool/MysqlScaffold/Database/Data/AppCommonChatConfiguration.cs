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
            .HasComment("id");
        entity.Property(e => e.AppId).HasComment("appid");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("current_timestamp()")
            .HasComment("创建时间");
        entity.Property(e => e.CreateUserId).HasComment("创建人");
        entity.Property(e => e.InputTokens).HasComment("输入token统计");
        entity.Property(e => e.IsDeleted).HasComment("软删除");
        entity.Property(e => e.OutTokens).HasComment("输出token统计");
        entity.Property(e => e.TeamId).HasComment("团队id");
        entity.Property(e => e.Title)
            .HasDefaultValueSql("'未命名标题'")
            .HasComment("对话标题");
        entity.Property(e => e.TotalTokens).HasComment("使用的 token 总数");
        entity.Property(e => e.UpdateTime)
            .ValueGeneratedOnAddOrUpdate()
            .HasDefaultValueSql("current_timestamp()")
            .HasComment("更新时间");
        entity.Property(e => e.UpdateUserId).HasComment("更新人");
        entity.Property(e => e.UserType).HasComment("用户类型");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<AppCommonChatEntity> modelBuilder);
}

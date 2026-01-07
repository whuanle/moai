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
/// 普通应用.
/// </summary>
public partial class AppCommonConfiguration : IEntityTypeConfiguration<AppCommonEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AppCommonEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("PRIMARY");

        entity.ToTable("app_common", tb => tb.HasComment("普通应用"));

        entity.Property(e => e.Id)
            .HasDefaultValueSql("unhex(replace(uuid(),'-',''))")
            .HasComment("id");
        entity.Property(e => e.AppId).HasComment("所属应用id");
        entity.Property(e => e.Avatar)
            .HasDefaultValueSql("''")
            .HasComment("AI头像,存 objectKey");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("current_timestamp()")
            .HasComment("创建时间");
        entity.Property(e => e.CreateUserId).HasComment("创建人");
        entity.Property(e => e.ExecutionSettings)
            .HasDefaultValueSql("'[]'")
            .HasComment("对话影响参数");
        entity.Property(e => e.IsAuth).HasComment("是否开启授权验证");
        entity.Property(e => e.IsDeleted).HasComment("软删除");
        entity.Property(e => e.ModelId).HasComment("对话使用的模型 id");
        entity.Property(e => e.Plugins)
            .HasDefaultValueSql("'[]'")
            .HasComment("要使用的插件");
        entity.Property(e => e.Prompt)
            .HasDefaultValueSql("''")
            .HasComment("提示词");
        entity.Property(e => e.TeamId).HasComment("团队id");
        entity.Property(e => e.UpdateTime)
            .ValueGeneratedOnAddOrUpdate()
            .HasDefaultValueSql("current_timestamp()")
            .HasComment("更新时间");
        entity.Property(e => e.UpdateUserId).HasComment("更新人");
        entity.Property(e => e.WikiIds)
            .HasDefaultValueSql("'0'")
            .HasComment("要使用的知识库id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<AppCommonEntity> modelBuilder);
}

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
/// ai模型.
/// </summary>
public partial class AiModelConfiguration : IEntityTypeConfiguration<AiModelEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AiModelEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("PRIMARY");

        entity.ToTable("ai_model", tb => tb.HasComment("ai模型"));

        entity.Property(e => e.Id).HasComment("id");
        entity.Property(e => e.AiModelType).HasComment("模型功能类型");
        entity.Property(e => e.AiProvider).HasComment("模型供应商");
        entity.Property(e => e.ContextWindowTokens)
            .HasDefaultValueSql("'8192'")
            .HasComment("上下文最大token数量");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("utc_timestamp()")
            .HasComment("创建时间");
        entity.Property(e => e.CreateUserId).HasComment("创建人");
        entity.Property(e => e.DeploymentName).HasComment("部署名称,Azure需要");
        entity.Property(e => e.Endpoint).HasComment("端点");
        entity.Property(e => e.Files).HasComment("支持文件上传");
        entity.Property(e => e.FunctionCall).HasComment("支持函数");
        entity.Property(e => e.ImageOutput).HasComment("支持图片输出");
        entity.Property(e => e.IsDeleted).HasComment("软删除");
        entity.Property(e => e.IsPublic).HasComment("是否开放给大家使用");
        entity.Property(e => e.IsVision).HasComment("支持计算机视觉");
        entity.Property(e => e.Key).HasComment("密钥");
        entity.Property(e => e.MaxDimension).HasComment("向量的维度");
        entity.Property(e => e.Name).HasComment("模型名称,gpt-4o");
        entity.Property(e => e.TextOutput)
            .HasDefaultValueSql("'8192'")
            .HasComment("最大文本输出token");
        entity.Property(e => e.Title).HasComment("自定义名模型名称，便于用户选择");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("utc_timestamp()")
            .HasComment("最后更新时间");
        entity.Property(e => e.UpdateUserId).HasComment("最后修改人");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<AiModelEntity> modelBuilder);
}

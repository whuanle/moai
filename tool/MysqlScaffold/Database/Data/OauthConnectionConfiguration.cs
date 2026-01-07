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
/// oauth2.0系统.
/// </summary>
public partial class OauthConnectionConfiguration : IEntityTypeConfiguration<OauthConnectionEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<OauthConnectionEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("PRIMARY");

        entity.ToTable("oauth_connection", tb => tb.HasComment("oauth2.0系统"));

        entity.Property(e => e.Id)
            .HasDefaultValueSql("unhex(replace(uuid(),'-',''))")
            .HasComment("id");
        entity.Property(e => e.AuthorizeUrl).HasComment("登录跳转地址");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("current_timestamp()")
            .HasComment("创建时间");
        entity.Property(e => e.CreateUserId).HasComment("创建人");
        entity.Property(e => e.IconUrl).HasComment("图标地址");
        entity.Property(e => e.IsDeleted).HasComment("软删除");
        entity.Property(e => e.Key).HasComment("应用key");
        entity.Property(e => e.Name).HasComment("认证名称");
        entity.Property(e => e.Provider).HasComment("提供商");
        entity.Property(e => e.Secret).HasComment("密钥");
        entity.Property(e => e.UpdateTime)
            .ValueGeneratedOnAddOrUpdate()
            .HasDefaultValueSql("current_timestamp()")
            .HasComment("更新时间");
        entity.Property(e => e.UpdateUserId).HasComment("更新人");
        entity.Property(e => e.WellKnown).HasComment("发现端口");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<OauthConnectionEntity> modelBuilder);
}

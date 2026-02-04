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
internal partial class OauthConnectionConfiguration : IEntityTypeConfiguration<OauthConnectionEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<OauthConnectionEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("idx_65771_primary");

        entity.ToTable("oauth_connection", tb => tb.HasComment("oauth2.0系统"));

        entity.Property(e => e.Id)
            .HasDefaultValueSql("uuid_generate_v4()")
            .HasComment("id")
            .HasColumnName("id");
        entity.Property(e => e.AuthorizeUrl)
            .HasMaxLength(1000)
            .HasComment("登录跳转地址")
            .HasColumnName("authorize_url");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .HasComment("创建时间")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasDefaultValue(0)
            .HasComment("创建人")
            .HasColumnName("create_user_id");
        entity.Property(e => e.IconUrl)
            .HasMaxLength(1000)
            .HasComment("图标地址")
            .HasColumnName("icon_url");
        entity.Property(e => e.IsDeleted)
            .HasDefaultValueSql("'0'::bigint")
            .HasComment("软删除")
            .HasColumnName("is_deleted");
        entity.Property(e => e.Key)
            .HasMaxLength(100)
            .HasComment("应用key")
            .HasColumnName("key");
        entity.Property(e => e.Name)
            .HasMaxLength(50)
            .HasComment("认证名称")
            .HasColumnName("name");
        entity.Property(e => e.Provider)
            .HasMaxLength(20)
            .HasComment("提供商")
            .HasColumnName("provider");
        entity.Property(e => e.Secret)
            .HasMaxLength(100)
            .HasComment("密钥")
            .HasColumnName("secret");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("更新时间")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasDefaultValue(0)
            .HasComment("更新人")
            .HasColumnName("update_user_id");
        entity.Property(e => e.WellKnown)
            .HasMaxLength(1000)
            .HasComment("发现端口")
            .HasColumnName("well_known");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<OauthConnectionEntity> modelBuilder);
}

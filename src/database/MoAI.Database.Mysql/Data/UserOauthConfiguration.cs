using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoAI.Database.Entities;

namespace MoAI.Database;

/// <summary>
/// oauth2.0对接.
/// </summary>
public partial class UserOauthConfiguration : IEntityTypeConfiguration<UserOauthEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<UserOauthEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("PRIMARY");

        entity.ToTable("user_oauth", tb => tb.HasComment("oauth2.0对接"));

        entity.Property(e => e.Id)
            .HasComment("id")
            .HasColumnType("int(11)")
            .HasColumnName("id");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("utc_timestamp()")
            .HasComment("创建时间")
            .HasColumnType("datetime")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("创建人")
            .HasColumnType("int(11)")
            .HasColumnName("create_user_id");
        entity.Property(e => e.IsDeleted)
            .HasComment("软删除")
            .HasColumnType("bigint(20)")
            .HasColumnName("is_deleted");
        entity.Property(e => e.ProviderId)
            .HasMaxLength(20)
            .HasComment("供应商id")
            .HasColumnName("provider_id");
        entity.Property(e => e.Sub)
            .HasMaxLength(50)
            .HasComment("用户id")
            .HasColumnName("sub");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("utc_timestamp()")
            .HasComment("最后更新时间")
            .HasColumnType("datetime")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasComment("最后修改人")
            .HasColumnType("int(11)")
            .HasColumnName("update_user_id");
        entity.Property(e => e.UserId)
            .HasComment("用户id")
            .HasColumnType("int(11)")
            .HasColumnName("user_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<UserOauthEntity> modelBuilder);
}

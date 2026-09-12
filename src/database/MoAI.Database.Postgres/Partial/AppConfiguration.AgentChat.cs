using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoAI.Database.Entities;

#pragma warning disable SA1600
#pragma warning disable SA1601
namespace MoAI.Database;

/// <summary>
/// 应用实体 Agent 对话扩展列的 EF 映射（发布状态/发布时间）。
/// </summary>
internal partial class AppConfiguration
{
    partial void OnConfigurePartial(EntityTypeBuilder<AppEntity> modelBuilder)
    {
        modelBuilder.Property(e => e.PublishStatus)
            .HasDefaultValue((short)0)
            .HasComment("发布状态，0=草稿 1=已发布")
            .HasColumnName("publish_status");

        modelBuilder.Property(e => e.PublishTime)
            .HasComment("发布时间，未发布为 null")
            .HasColumnName("publish_time");
    }
}

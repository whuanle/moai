using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoAI.Database.Entities;

#pragma warning disable SA1600
#pragma warning disable SA1601
namespace MoAI.Database;

/// <summary>
/// Agent 应用会话实体冷快照列的 EF 映射。
/// </summary>
internal partial class AppAgentSessionConfiguration
{
    partial void OnConfigurePartial(EntityTypeBuilder<AppAgentSessionEntity> modelBuilder)
    {
        modelBuilder.Property(e => e.State)
            .HasColumnType("jsonb")
            .HasComment("Agent 会话冷快照（AgentSession 序列化，含上下文压缩索引），Redis 热态失效后恢复")
            .HasColumnName("state");
    }
}

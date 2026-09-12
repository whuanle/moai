using System;

#pragma warning disable CA1051
#pragma warning disable SA1401
#pragma warning disable SA1600
#pragma warning disable SA1601
#pragma warning disable SA1204
namespace MoAI.Database.Entities;

/// <summary>
/// 应用实体的 Agent 对话扩展列（发布状态/发布时间）。
/// <para>放在 <c>Partial/</c> 目录而非 <c>Entities/</c>，避免 PostgresScaffold 分发时（先删后拷）被覆盖。</para>
/// </summary>
public partial class AppEntity
{
    /// <summary>
    /// 发布状态：0=草稿（未发布）1=已发布.
    /// </summary>
    public short PublishStatus { get; set; }

    /// <summary>
    /// 发布时间，未发布为 null.
    /// </summary>
    public DateTimeOffset? PublishTime { get; set; }
}

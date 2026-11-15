namespace MoAI.Database.Schema;

/// <summary>
/// 一次 schema 补全的结果.
/// </summary>
public sealed class SchemaSyncResult
{
    /// <summary>
    /// 新建的表.
    /// </summary>
    public List<string> CreatedTables { get; } = [];

    /// <summary>
    /// 新增的列（表.列）.
    /// </summary>
    public List<string> AddedColumns { get; } = [];

    /// <summary>
    /// 加宽的列（表.列）.
    /// </summary>
    public List<string> WidenedColumns { get; } = [];

    /// <summary>
    /// 新建的索引.
    /// </summary>
    public List<string> CreatedIndexes { get; } = [];

    /// <summary>
    /// 非致命失败（索引建失败等，记录后继续启动）.
    /// </summary>
    public List<string> Warnings { get; } = [];

    /// <summary>
    /// 是否有实际变更.
    /// </summary>
    public bool HasChanges => CreatedTables.Count > 0 || AddedColumns.Count > 0 || WidenedColumns.Count > 0 || CreatedIndexes.Count > 0;
}

namespace MoAI.Database.Schema;

/// <summary>
/// 表结构定义：由 EF Core <c>GenerateCreateScript()</c> 输出解析而来，用于存量库补表/补列.
/// </summary>
public sealed class TableDefinition
{
    /// <summary>
    /// 表名（与生成脚本一致，保留字带双引号）.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 表名（去掉双引号，用于与 information_schema 比较）.
    /// </summary>
    public string RawName { get; init; } = string.Empty;

    /// <summary>
    /// 原始 <c>CREATE TABLE</c> 语句.
    /// </summary>
    public string CreateStatement { get; init; } = string.Empty;

    /// <summary>
    /// 列定义.
    /// </summary>
    public IReadOnlyList<ColumnDefinition> Columns { get; init; } = [];

    /// <summary>
    /// 该表的索引语句（<c>CREATE [UNIQUE] INDEX ...</c>）.
    /// </summary>
    public IReadOnlyList<string> IndexStatements { get; init; } = [];
}

/// <summary>
/// 列定义.
/// </summary>
public sealed class ColumnDefinition
{
    /// <summary>
    /// 列名（保留字带双引号）.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 列名（去掉双引号）.
    /// </summary>
    public string RawName { get; init; } = string.Empty;

    /// <summary>
    /// 完整列定义（类型 + 约束），可直接用于 <c>ALTER TABLE ... ADD COLUMN</c>.
    /// </summary>
    public string Definition { get; init; } = string.Empty;

    /// <summary>
    /// 存储类型（不含约束），如 <c>character varying(50)</c>.
    /// </summary>
    public string TypeName { get; init; } = string.Empty;

    /// <summary>
    /// 是否有 DEFAULT.
    /// </summary>
    public bool HasDefault { get; init; }

    /// <summary>
    /// 是否 NOT NULL.
    /// </summary>
    public bool IsRequired { get; init; }

    /// <summary>
    /// 是否自增列（IDENTITY / serial）.
    /// </summary>
    public bool IsIdentity { get; init; }

    /// <summary>
    /// <c>character varying(n)</c> 的长度上限，其它类型为 null.
    /// </summary>
    public int? MaxLength { get; init; }
}

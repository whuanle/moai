using System.Collections.Generic;

namespace MoAI.AIPlugin.Dynamic;

/// <summary>
/// SQL 查询结果的中立表示（列名 + 行 + 是否截断），由 <see cref="SqlResultReader"/> 产出，供各 SQL 插件映射为各自的响应模型.
/// </summary>
/// <param name="Columns">结果集列名（按查询返回顺序）.</param>
/// <param name="Rows">结果行，每行为「列名 → 值」的映射.</param>
/// <param name="Truncated">是否因行数上限被截断（为 <see langword="true"/> 表示数据库中还有更多行未返回）.</param>
internal sealed record SqlQueryResult(
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows,
    bool Truncated);

using System;
using System.Collections.Generic;

namespace MoAI.Memory.Queries.Responses;

/// <summary>
/// 全局记忆可选向量化模型响应.
/// </summary>
public class QueryGlobalMemoryModelOptionsResponse
{
    /// <summary>
    /// 可选模型列表（公开启用中的 embedding 模型）.
    /// </summary>
    public IReadOnlyList<GlobalMemoryModelOption> Items { get; init; } = [];
}

/// <summary>
/// 全局记忆可选模型条目.
/// </summary>
public class GlobalMemoryModelOption
{
    /// <summary>
    /// 模型 id.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// 模型名称.
    /// </summary>
    public string Name { get; init; } = string.Empty;
}

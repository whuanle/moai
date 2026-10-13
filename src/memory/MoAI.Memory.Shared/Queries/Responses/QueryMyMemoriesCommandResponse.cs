using System;
using System.Collections.Generic;
using MoAI.Memory.Models;

namespace MoAI.Memory.Queries.Responses;

/// <summary>
/// 本人记忆列表响应.
/// </summary>
public class QueryMyMemoriesCommandResponse
{
    /// <summary>
    /// 记忆条目.
    /// </summary>
    public IReadOnlyList<MemoryItem> Items { get; init; } = [];

    /// <summary>
    /// 总条数.
    /// </summary>
    public int Total { get; init; }

    /// <summary>
    /// 应用是否开启记忆（未开启时前端隐藏管理入口）.
    /// </summary>
    public bool MemoryEnabled { get; init; }

}

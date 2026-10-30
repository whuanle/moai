using System;
using System.Collections.Generic;
using MoAI.Memory.Models;

namespace MoAI.Memory.Queries.Responses;

/// <summary>
/// 本人全局记忆列表响应.
/// </summary>
public class QueryMyGlobalMemoriesCommandResponse
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
    /// 平台是否开启全局记忆（未开启时前端展示引导提示）.
    /// </summary>
    public bool GlobalEnabled { get; init; }
}

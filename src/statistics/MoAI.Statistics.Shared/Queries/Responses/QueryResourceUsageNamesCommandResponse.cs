using System.Collections.Generic;

namespace MoAI.Statistics.Queries.Responses;

/// <summary>
/// 资源类型名清单响应.
/// </summary>
public class QueryResourceUsageNamesCommandResponse
{
    /// <summary>
    /// 资源类型名集合（去重升序）.
    /// </summary>
    public IReadOnlyList<string> Items { get; set; } = new List<string>();
}

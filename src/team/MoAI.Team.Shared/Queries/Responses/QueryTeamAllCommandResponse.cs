using System;
using System.Collections.Generic;

namespace MoAI.Team.Queries.Responses;

/// <summary>
/// 全部团队列表响应（管理员）.
/// </summary>
public class QueryTeamAllCommandResponse
{
    /// <summary>
    /// 团队集合.
    /// </summary>
    public IReadOnlyList<QueryTeamAllCommandResponseItem> Items { get; set; } = new List<QueryTeamAllCommandResponseItem>();
}

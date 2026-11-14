using System;
using System.Collections.Generic;

namespace MoAI.AIChannel.Queries.Responses;

/// <summary>
/// 团队可用模型列表响应.
/// </summary>
public class QueryTeamAvailableModelsCommandResponse
{
    /// <summary>
    /// 模型集合.
    /// </summary>
    public IReadOnlyList<TeamAvailableModelItem> Items { get; set; } = new List<TeamAvailableModelItem>();
}

using System.Collections.Generic;

namespace MoAI.Settings.Queries.Responses;

/// <summary>
/// 查询设置项响应.
/// </summary>
public class QuerySettingsCommandResponse
{
    /// <summary>
    /// 设置项集合.
    /// </summary>
    public IReadOnlyList<SettingItemResponse> Items { get; init; } = new List<SettingItemResponse>();
}

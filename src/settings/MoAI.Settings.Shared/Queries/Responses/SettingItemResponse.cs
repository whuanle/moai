namespace MoAI.Settings.Queries.Responses;

/// <summary>
/// 设置项.
/// </summary>
public class SettingItemResponse
{
    /// <summary>
    /// 设置项 key.
    /// </summary>
    public string Key { get; init; } = default!;

    /// <summary>
    /// 设置项名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 设置项描述.
    /// </summary>
    public string Description { get; init; } = default!;

    /// <summary>
    /// 设置项当前值.
    /// </summary>
    public string Value { get; init; } = default!;
}

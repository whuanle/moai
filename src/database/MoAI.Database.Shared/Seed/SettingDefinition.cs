namespace MoAI.Database.Seed;

/// <summary>
/// 设置项定义.
/// </summary>
public sealed class SettingDefinition
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
    /// 默认值，当数据库中没有对应记录时使用.
    /// </summary>
    public string DefaultValue { get; init; } = default!;
}

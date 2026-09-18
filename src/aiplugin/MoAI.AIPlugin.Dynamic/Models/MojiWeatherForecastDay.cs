using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// 墨迹天气未来逐日预报（上游原样字符串值）.
/// </summary>
public class MojiWeatherForecastDay
{
    /// <summary>
    /// 预报日期.
    /// </summary>
    [Description("预报日期，如 2026-09-18")]
    public string? Date { get; init; }

    /// <summary>
    /// 星期.
    /// </summary>
    [Description("星期，如 周五")]
    public string? Week { get; init; }

    /// <summary>
    /// 白天天气现象.
    /// </summary>
    [Description("白天天气现象")]
    public string? ConditionDay { get; init; }

    /// <summary>
    /// 夜间天气现象.
    /// </summary>
    [Description("夜间天气现象")]
    public string? ConditionNight { get; init; }

    /// <summary>
    /// 白天最高温度.
    /// </summary>
    [Description("白天最高温度")]
    public string? TempDay { get; init; }

    /// <summary>
    /// 夜间最低温度.
    /// </summary>
    [Description("夜间最低温度")]
    public string? TempNight { get; init; }

    /// <summary>
    /// 白天风向.
    /// </summary>
    [Description("白天风向")]
    public string? WindDirDay { get; init; }

    /// <summary>
    /// 白天风力等级.
    /// </summary>
    [Description("白天风力等级")]
    public string? WindLevelDay { get; init; }

    /// <summary>
    /// 夜间风向.
    /// </summary>
    [Description("夜间风向")]
    public string? WindDirNight { get; init; }

    /// <summary>
    /// 夜间风力等级.
    /// </summary>
    [Description("夜间风力等级")]
    public string? WindLevelNight { get; init; }

    /// <summary>
    /// 日出时间.
    /// </summary>
    [Description("日出时间")]
    public string? SunRise { get; init; }

    /// <summary>
    /// 日落时间.
    /// </summary>
    [Description("日落时间")]
    public string? SunSet { get; init; }
}

using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// 墨迹天气实时天气（上游原样字符串值）.
/// </summary>
public class MojiWeatherCondition
{
    /// <summary>
    /// 实时温度.
    /// </summary>
    [Description("实时温度")]
    public string? Temp { get; init; }

    /// <summary>
    /// 天气现象（晴/多云/小雨等）.
    /// </summary>
    [Description("天气现象（晴/多云/小雨等）")]
    public string? Text { get; init; }

    /// <summary>
    /// 湿度.
    /// </summary>
    [Description("湿度")]
    public string? Humidity { get; init; }

    /// <summary>
    /// 风向.
    /// </summary>
    [Description("风向")]
    public string? WindDir { get; init; }

    /// <summary>
    /// 风力等级.
    /// </summary>
    [Description("风力等级")]
    public string? WindLevel { get; init; }

    /// <summary>
    /// 风速.
    /// </summary>
    [Description("风速")]
    public string? WindSpeed { get; init; }

    /// <summary>
    /// 气压.
    /// </summary>
    [Description("气压")]
    public string? Pressure { get; init; }

    /// <summary>
    /// 天气现象图标代码.
    /// </summary>
    [Description("天气现象图标代码")]
    public string? Icon { get; init; }

    /// <summary>
    /// 实况更新时间.
    /// </summary>
    [Description("实况更新时间")]
    public string? UpDateTime { get; init; }
}

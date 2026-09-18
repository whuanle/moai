using System.Collections.Generic;
using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// 墨迹天气响应结果.
/// </summary>
public class MojiWeatherResponse
{
    /// <summary>
    /// 上游解析出的定位城市信息.
    /// </summary>
    [Description("上游解析出的定位城市信息（未返回时为 null）")]
    public MojiWeatherCity? City { get; init; }

    /// <summary>
    /// 实时天气.
    /// </summary>
    [Description("实时天气（温度/天气现象/湿度/风向风力等）")]
    public MojiWeatherCondition? Condition { get; init; }

    /// <summary>
    /// 未来逐日预报.
    /// </summary>
    [Description("未来逐日预报列表")]
    public IReadOnlyList<MojiWeatherForecastDay> Forecast { get; init; } = [];
}

using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// 墨迹天气返回的定位城市信息.
/// </summary>
public class MojiWeatherCity
{
    /// <summary>
    /// 墨迹城市 ID.
    /// </summary>
    [Description("墨迹城市 ID")]
    public string? CityId { get; init; }

    /// <summary>
    /// 城市名（区/县名）.
    /// </summary>
    [Description("城市名（区/县名）")]
    public string? Name { get; init; }

    /// <summary>
    /// 省/直辖市名.
    /// </summary>
    [Description("省/直辖市名")]
    public string? Province { get; init; }

    /// <summary>
    /// 国家名.
    /// </summary>
    [Description("国家名")]
    public string? Country { get; init; }
}

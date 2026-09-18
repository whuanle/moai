using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// 墨迹天气请求参数.
/// </summary>
public class MojiWeatherRequest
{
    /// <summary>
    /// 墨迹城市 ID.
    /// </summary>
    [Description("墨迹城市 ID（与 Lat/Lon 二选一；城市 ID 列表在购买服务后的云市场控制台可下载）")]
    public string? CityId { get; set; }

    /// <summary>
    /// 纬度.
    /// </summary>
    [Description("纬度，如 39.90598；与 Lon 同时提供，与 CityId 二选一")]
    public string? Lat { get; set; }

    /// <summary>
    /// 经度.
    /// </summary>
    [Description("经度，如 116.39139；与 Lat 同时提供，与 CityId 二选一")]
    public string? Lon { get; set; }
}

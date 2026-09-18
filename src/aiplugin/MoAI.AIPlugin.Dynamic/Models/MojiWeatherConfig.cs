using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// 墨迹天气插件配置（每个实例独立保存）.
/// </summary>
public class MojiWeatherConfig
{
    /// <summary>
    /// 阿里云云市场 AppCode.
    /// </summary>
    [Description("阿里云云市场 AppCode：购买墨迹天气 API 服务后，在云市场「已购买服务」的控制台查看；调用时以「APPCODE 空格 AppCode」形式放入 Authorization 头")]
    public string AppCode { get; set; } = string.Empty;

    /// <summary>
    /// 墨迹天气访问令牌（部分服务规格要求）.
    /// </summary>
    [Description("访问令牌 Token：部分墨迹服务规格要求随表单下发（购买后云市场控制台可查）；不要求的规格留空")]
    public string? Token { get; set; }
}

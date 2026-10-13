using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// 新闻热榜插件配置（每个实例独立保存）.
/// </summary>
public class HotBoardConfig
{
    /// <summary>
    /// 每个来源返回的条数上限.
    /// </summary>
    [Description("每个来源返回的条数上限，1-50，默认 20")]
    public int Limit { get; set; } = 20;

    /// <summary>
    /// 调试网关地址（可选）.
    /// </summary>
    [Description("调试网关地址（可选）：设置后所有平台请求改写为 {Gateway}/https/{域名}/{路径}，用于镜像、抓包或本地桩服务；留空直连各平台官方地址")]
    public string? Gateway { get; set; }
}

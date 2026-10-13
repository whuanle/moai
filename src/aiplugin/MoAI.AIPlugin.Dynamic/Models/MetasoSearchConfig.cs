using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// 秘塔搜索插件配置（每个实例独立保存）.
/// </summary>
public class MetasoSearchConfig
{
    /// <summary>
    /// 秘塔 API Key.
    /// </summary>
    [Description("秘塔 API Key，前往 https://metaso.cn/search-api/api-keys 获取（形如 mk-xxxx）")]
    public string ApiKey { get; set; } = string.Empty;
}

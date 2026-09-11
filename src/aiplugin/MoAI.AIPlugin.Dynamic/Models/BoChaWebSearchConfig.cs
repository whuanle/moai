using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// 博查全网搜索插件配置（每个实例独立保存）.
/// </summary>
public class BoChaWebSearchConfig
{
    /// <summary>
    /// 博查开放平台 API Key（Bearer 之后的密钥部分）.
    /// </summary>
    [Description("博查开放平台 API Key，前往 https://open.bocha.cn 的「API KEY 管理」获取")]
    public string ApiKey { get; set; } = string.Empty;
}

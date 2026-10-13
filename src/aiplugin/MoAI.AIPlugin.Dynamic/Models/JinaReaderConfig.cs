using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// Jina 网页阅读插件配置（每个实例独立保存）.
/// </summary>
public class JinaReaderConfig
{
    /// <summary>
    /// Jina AI API Key.
    /// </summary>
    [Description("Jina AI API Key，前往 https://jina.ai/ 获取（形如 jina_xxxx）")]
    public string ApiKey { get; set; } = string.Empty;
}

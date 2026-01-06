using System.Text.Json.Serialization;

namespace MoAI.Plugin.Plugins.BailianRetrieve;

/// <summary>
/// 阿里云百炼知识库检索运行参数.
/// </summary>
public class BailianRetrieveParams
{
    /// <summary>
    /// 查询提示词.
    /// </summary>
    [JsonPropertyName("Query")]
    public string Query { get; set; } = string.Empty;
}

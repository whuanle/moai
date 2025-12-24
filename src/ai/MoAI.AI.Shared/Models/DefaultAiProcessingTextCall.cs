using System.Text;
using System.Text.Json.Serialization;

namespace MoAI.AI.Models;

/// <summary>
/// 默认文本处理调用.
/// </summary>
public class DefaultAiProcessingTextCall : AiProcessingTextCall
{
    /// <summary>
    /// 文本.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 内容构建器.
    /// </summary>
    [JsonIgnore]
    public StringBuilder ContentBuilder { get; set; } = new StringBuilder();

    /// <summary>
    /// 刷新.
    /// </summary>
    public void Refresh()
    {
        Content = ContentBuilder.ToString();
    }
}

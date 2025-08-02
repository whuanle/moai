using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MoAI.App.AIAssistant.Models;

/// <summary>
/// 内容类型.
/// </summary>
public enum AiContentType
{
    /// <summary>
    /// 文本内容
    /// </summary>
    [JsonPropertyName("text")]
    Text = 0,

    /// <summary>
    /// 插件调用
    /// </summary>
    [JsonPropertyName("plugin")]
    Plugin = 1,

    /// <summary>
    /// 知识库搜索.
    /// </summary>
    [JsonPropertyName("wiki")]
    Wiki = 2,
}

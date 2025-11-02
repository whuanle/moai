using System.Text.Json.Serialization;

namespace MoAI.Infra.Feishu.Models;

public class FeishuWebHookPost
{
    /// <summary>
    /// 富文本消息内容，包含中英文配置.
    /// </summary>
    [JsonPropertyName("post")]
    public PostMessage Post { get; set; }

    public class PostMessage
    {
        /// <summary>
        /// 富文本消息的中文配置.
        /// </summary>
        [JsonPropertyName("zh_cn")]
        public LanguageConfig ZhCn { get; set; }

        /// <summary>
        /// 富文本消息的英文配置.
        /// </summary>
        [JsonPropertyName("en_us")]
        public LanguageConfig EnUs { get; set; }
    }

    public class LanguageConfig
    {
        /// <summary>
        /// 富文本消息的标题.
        /// </summary>
        [JsonPropertyName("title")]
        public string Title { get; set; }

        /// <summary>
        /// 富文本消息内容，由多个段落组成，每个段落包含若干节点.
        /// </summary>
        [JsonPropertyName("content")]
        public List<List<RichTextElement>> Content { get; set; }
    }

    public class RichTextElement
    {
        /// <summary>
        /// 标签类型，例如 "text"、"a"、"at"、"img".
        /// </summary>
        [JsonPropertyName("tag")]
        public string Tag { get; set; }

        /// <summary>
        /// 文本内容，仅在标签类型为 "text" 或 "a" 时必填.
        /// </summary>
        [JsonPropertyName("text")]
        public string Text { get; set; }

        /// <summary>
        /// 超链接地址，仅在标签类型为 "a" 时必填.
        /// </summary>
        [JsonPropertyName("href")]
        public string Href { get; set; }

        /// <summary>
        /// 用户的 Open ID 或 User ID，仅在标签类型为 "at" 时必填.
        /// </summary>
        [JsonPropertyName("user_id")]
        public string UserId { get; set; }

        /// <summary>
        /// 用户名称，仅在标签类型为 "at" 时可选.
        /// </summary>
        [JsonPropertyName("user_name")]
        public string UserName { get; set; }

        /// <summary>
        /// 图片的唯一标识，仅在标签类型为 "img" 时必填.
        /// </summary>
        [JsonPropertyName("image_key")]
        public string ImageKey { get; set; }

        /// <summary>
        /// 是否 unescape 解码，默认值为 false.
        /// </summary>
        [JsonPropertyName("un_escape")]
        public bool? UnEscape { get; set; }
    }
}
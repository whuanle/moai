using System.Text.Json.Serialization;

namespace MoAI.Infra.Feishu.Models;

public class FeishuWebHookInteractiveData
{
    /// <summary>
    /// 卡片类型。要发送由搭建工具搭建的卡片（也称卡片模板），固定取值为 template。
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "template";

    /// <summary>
    /// 卡片模板的数据，要发送由搭建工具搭建的卡片，此处需传入卡片模板 ID、卡片版本号等。
    /// </summary>
    [JsonPropertyName("data")]
    public TemplateData Data { get; set; }

    public class TemplateData
    {
        /// <summary>
        /// 搭建工具中创建的卡片（也称卡片模板）的 ID，如 AAqigYkzabcef。
        /// 可在搭建工具中通过复制卡片模板 ID 获取。
        /// </summary>
        [JsonPropertyName("template_id")]
        public string TemplateId { get; set; }

        /// <summary>
        /// 搭建平台中创建的卡片的版本号，如 1.0.0。
        /// 卡片发布后，将生成版本号。可在搭建工具 版本管理 处获取。
        /// 注意：若不填此字段，将默认使用该卡片的最新版本。
        /// </summary>
        [JsonPropertyName("template_version_name")]
        public string TemplateVersionName { get; set; }

        /// <summary>
        /// 若卡片绑定了变量，你需在该字段中传入实际变量数据的值。
        /// 示例：如果变量名称在搭建工具中被定义为 open_id，此处需要对 open_id 变量传入值：
        /// {
        ///     "open_id": "ou_d506829e8b6a17607e56bcd6b1aabcef"
        /// }
        /// </summary>
        [JsonPropertyName("template_variable")]
        public Dictionary<string, object> TemplateVariable { get; set; }
    }
}

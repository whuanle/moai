using System.Text.Json.Serialization;

namespace MoAI.App.Models;

/// <summary>
/// 应用外部渠道类型（应用的对外服务入口，用户可从第三方平台直接对话应用）.
/// </summary>
public enum AppExternalChannelType
{
    /// <summary>
    /// 飞书：通过飞书应用连接（feishu_app）绑定应用渠道，用户在飞书机器人单聊中对话应用.
    /// </summary>
    [JsonPropertyName("feishu")]
    Feishu = 0,

    /// <summary>
    /// 钉钉：通过钉钉企业内部应用（dingtalk_app）绑定应用渠道，用户在钉钉机器人单聊/群聊中对话应用.
    /// </summary>
    [JsonPropertyName("dingtalk")]
    Dingtalk = 1,
}

using System.Text.Json.Serialization;

namespace MoAI.Infra.Feishu.Models;

public static class FeishuWebHookMsgType
{
    /// <summary>
    /// 富文本.
    /// </summary>
    public const string Post = "post";

    /// <summary>
    /// 普通文本.
    /// </summary>
    public const string Text = "text";

    /// <summary>
    /// 群名片.
    /// </summary>
    public const string ShareChat = "share_chat";

    /// <summary>
    /// 图片.
    /// </summary>
    public const string Image = "image";

    /// <summary>
    /// 卡片消息.
    /// </summary>
    public const string Interactive = "interactive";
}
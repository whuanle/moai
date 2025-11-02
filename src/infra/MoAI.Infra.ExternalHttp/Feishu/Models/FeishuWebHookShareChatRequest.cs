using System.Text.Json.Serialization;

namespace MoAI.Infra.Feishu.Models;
public class FeishuWebHookShareChatRequest: FeishuWebHookRequest<FeishuWebHookShareChat>
{
    /// <inheritdoc/>
    public override string MsgType => FeishuWebHookMsgType.ShareChat;
}

using System.Text.Json.Serialization;

namespace MoAI.Infra.Feishu.Models;

public class FeishuWebHookPostRequest : FeishuWebHookRequest<FeishuWebHookPost>
{
    /// <inheritdoc/>
    public override string MsgType => FeishuWebHookMsgType.Post;
}

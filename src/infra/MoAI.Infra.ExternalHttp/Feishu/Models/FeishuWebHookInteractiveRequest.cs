using System.Text.Json.Serialization;

namespace MoAI.Infra.Feishu.Models;

public class FeishuWebHookInteractiveRequest : FeishuWebHookRequest<FeishuWebHookInteractiveData>
{
    /// <inheritdoc/>
    public override string MsgType => FeishuWebHookMsgType.Interactive;
}

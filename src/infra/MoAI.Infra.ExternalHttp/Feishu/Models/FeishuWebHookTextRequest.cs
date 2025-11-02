using System.Text.Json.Serialization;

namespace MoAI.Infra.Feishu.Models;

public class FeishuWebHookTextRequest: FeishuWebHookRequest<FeishuWebHookText>
{
    /// <inheritdoc/>
    public override string MsgType => FeishuWebHookMsgType.Text;
}

using System.Text.Json.Serialization;

namespace MoAI.Infra.Feishu.Models;

public class FeishuWebHookImageRequest : FeishuWebHookRequest<FeishuWebHookImage>
{
    /// <inheritdoc/>
    public override string MsgType => FeishuWebHookMsgType.Image;
}

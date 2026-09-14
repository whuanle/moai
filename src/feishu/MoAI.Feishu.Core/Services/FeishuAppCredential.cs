namespace MoAI.Feishu.Services;

/// <summary>
/// 飞书应用凭证.
/// </summary>
/// <param name="Domain">接入域名.</param>
/// <param name="AppId">飞书开放平台 AppID.</param>
/// <param name="AppSecret">飞书开放平台 AppSecret.</param>
public sealed record FeishuAppCredential(string Domain, string AppId, string AppSecret);

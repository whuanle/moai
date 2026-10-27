namespace MoAI.WeixinWork.Services;

/// <summary>
/// 企业微信应用凭证（连接配置快照），用于获取 access_token 与拼接 API 地址.
/// </summary>
/// <param name="Id">企业微信应用记录 id.</param>
/// <param name="CorpId">企业 ID.</param>
/// <param name="CorpSecret">应用 Secret.</param>
/// <param name="AgentId">应用 AgentId.</param>
/// <param name="Endpoint">API 地址（已归一化，无尾部斜杠）.</param>
public sealed record WeixinWorkAppCredential(Guid Id, string CorpId, string CorpSecret, int AgentId, string Endpoint);

/// <summary>
/// 企业微信 access_token 获取结果.
/// </summary>
/// <param name="AccessToken">访问凭证.</param>
/// <param name="Credential">凭证对应的连接配置快照.</param>
public sealed record WeixinWorkAccessTokenResult(string AccessToken, WeixinWorkAppCredential Credential);

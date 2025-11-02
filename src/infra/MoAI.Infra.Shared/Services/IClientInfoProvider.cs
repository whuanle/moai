namespace MoAI.Infra.Services;

/// <summary>
/// IClientInfoProvider.
/// </summary>
public interface IClientInfoProvider
{
    /// <summary>
    /// 获取客户端信息.
    /// </summary>
    /// <returns></returns>
    MoAI.Infra.Models.ClientInfo GetClientInfo();
}
#pragma warning disable CS1591 // 缺少对公共可见类型或成员的 XML 注释
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable CA1002 // 不要公开泛型列表
#pragma warning disable CA2227 // 集合属性应为只读

using MoAI.Infra.Exceptions;
using MoAI.Infra.Feishu.Models;
using Refit;

namespace MoAI.Infra.Feishu;

/// <summary>
/// 飞书接口.
/// </summary>
public partial interface IFeishuApiClient
{
    public HttpClient Client { get; }

    /// <summary>
    /// 自建应用获取 tenant_access_token.
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    [Post("/open-apis/auth/v3/tenant_access_token/internal")]
    Task<FeishuTenantAccessTokenResponse> GetTenantAccessTokenAsync([Body(BodySerializationMethod.Serialized)] FeishuTenantAccessTokenRequest request);

    public void CheckCode<T>(T response)
        where T : FeishuCode
    {
        if (response.Code != 0)
        {
            throw new BusinessException("飞书接口调用异常，({0}){1}", response.Code, response.Msg);
        }
    }
}
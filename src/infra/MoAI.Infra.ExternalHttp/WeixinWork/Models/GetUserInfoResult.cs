
#pragma warning disable CS1591 // 缺少对公共可见类型或成员的 XML 注释
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable CA1002 // 不要公开泛型列表
#pragma warning disable CA2227 // 集合属性应为只读
using System.Text.Json.Serialization;

namespace MoAI.Infra.WeixinWork.Models;

/// <summary>
/// 获取用户身份接口返回结果
/// </summary>
public class GetUserInfoResult
{
    [JsonPropertyName("errcode")]
    public int ErrCode { get; set; }

    [JsonPropertyName("errmsg")]
    public string ErrMsg { get; set; }

    /// <summary>
    /// 企业成员 userid.
    /// </summary>
    [JsonPropertyName("userid")]
    public string? UserId { get; set; }

    /// <summary>
    /// 敏感信息授权（snsapi_privateinfo）时返回，用于 getuserdetail.
    /// </summary>
    [JsonPropertyName("user_ticket")]
    public string? UserTicket { get; set; }

    // 非企业成员字段
    [JsonPropertyName("openid")]
    public string? OpenId { get; set; }

    [JsonPropertyName("external_userid")]
    public string? ExternalUserId { get; set; }
}

#pragma warning disable CS1591 // 缺少对公共可见类型或成员的 XML 注释
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
#pragma warning disable SA1600 // Elements should be documented
using System.Text.Json.Serialization;

namespace MoAI.Infra.WeixinWork.Models;

/// <summary>
/// 根据 code 获取成员信息的请求体.
/// </summary>
public class GetUserInfoByCodeRequest
{
    /// <summary>
    /// 成员授权获取到的 code，最大 5KB.
    /// </summary>
    [JsonPropertyName("code")]
    public string Code { get; set; } = default!;
}

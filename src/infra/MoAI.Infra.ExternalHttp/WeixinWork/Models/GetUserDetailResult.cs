
#pragma warning disable CS1591 // 缺少对公共可见类型或成员的 XML 注释
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable CA1002 // 不要公开泛型列表
#pragma warning disable CA2227 // 集合属性应为只读
namespace MoAI.Infra.WeixinWork.Models;

/// <summary>
/// 获取访问用户敏感信息返回结果.
/// </summary>
public class GetUserDetailResult
{
    public int ErrCode { get; set; }

    public string ErrMsg { get; set; }

    public string? UserId { get; set; }

    public string? Gender { get; set; }

    public string? Avatar { get; set; }

    public string? QrCode { get; set; }

    public string? Mobile { get; set; }

    public string? Email { get; set; }

    public string? BizMail { get; set; }

    public string? Address { get; set; }
}


#pragma warning disable CS1591 // 缺少对公共可见类型或成员的 XML 注释
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable CA1002 // 不要公开泛型列表
#pragma warning disable CA2227 // 集合属性应为只读
namespace MoAI.Infra.DingTalk;

public class ContactUserInfoResponse
{
    public string? Nick { get; set; }

    public string? AvatarUrl { get; set; }

    public string? Mobile { get; set; }

    public string? OpenId { get; set; }

    public string? UnionId { get; set; }

    public string? Email { get; set; }

    public string? StateCode { get; set; }
}
#pragma warning disable CS1591 // 缺少对公共可见类型或成员的 XML 注释
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable CA1002 // 不要公开泛型列表
#pragma warning disable CA2227 // 集合属性应为只读

using Refit;

namespace MoAI.Infra.Feishu.Models;

/// <summary>
/// 获取知识空间列表的请求.
/// </summary>
public class GetWikiSpacesRequest
{
    /// <summary>
    /// 分页大小.
    /// </summary>
    /// <example>10</example>
    [AliasAs("page_size")]
    public int? PageSize { get; init; }

    /// <summary>
    /// 分页标记，第一次请求不填，表示从头开始遍历.
    /// </summary>
    /// <example>1565676577122621</example>
    [AliasAs("page_token")]
    public string? PageToken { get; init; }
}

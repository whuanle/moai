#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。

namespace MoAI.Infra.Metaso.Models;

/// <summary>
/// 秘塔搜索 API 响应：按 scope 命中的结果分集合返回（image 为 images，其余为对应集合），开启摘要召回时带综合摘要.
/// </summary>
public class MetasoSearchPayload
{
    /// <summary>
    /// 网页搜索结果.
    /// </summary>
    public List<MetasoWebpageResult>? Webpages { get; init; }

    /// <summary>
    /// 文库搜索结果.
    /// </summary>
    public List<MetasoDocumentResult>? Documents { get; init; }

    /// <summary>
    /// 图片搜索结果.
    /// </summary>
    public List<MetasoImageResult>? Images { get; init; }

    /// <summary>
    /// 视频搜索结果（与文库结果字段同构）.
    /// </summary>
    public List<MetasoDocumentResult>? Videos { get; init; }

    /// <summary>
    /// 命中结果总数.
    /// </summary>
    public int? Total { get; init; }

    /// <summary>
    /// 综合摘要（仅 includeSummary=true 时返回）.
    /// </summary>
    public string? Summary { get; init; }

    /// <summary>
    /// 本次调用消耗的点数.
    /// </summary>
    public int? Credits { get; init; }
}

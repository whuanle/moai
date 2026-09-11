using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// PP-StructureV3 文档解析响应结果.
/// </summary>
/// <remarks>
/// 按页聚合 <c>LayoutParsingResults</c>，每页对应一份版面解析结果；印章文本单独从 <c>seal_res_list</c> 抽出，
/// 方便上游在不开印章识别子产线时也能零成本跳过；其它结构化字段以原始 JSON 文本回传，由调用方按需解析.
/// </remarks>
public class PaddleStructureV3Response
{
    /// <summary>
    /// 按页聚合的版面解析结果.
    /// </summary>
    [Description("按页聚合的版面解析结果列表；顺序与 PaddleOCR 服务端返回的 layoutParsingResults 一致")]
    public IReadOnlyList<PaddleStructureV3Page> Pages { get; init; } = [];
}
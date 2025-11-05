using System.Text.Json.Serialization;

namespace MoAI.Infra.Doc2x.Models;

/// <summary>
/// 导出请求参数
/// </summary>
public class Doc2xExportRequest
{
    /// <summary>
    /// 解析任务的 id
    /// </summary>
    [JsonPropertyName("uid")]
    public string Uid { get; set; }

    /// <summary>
    /// 导出格式，支持：md|tex|docx
    /// </summary>
    [JsonPropertyName("to")]
    public string To { get; set; }

    /// <summary>
    /// 导出模型，需填写：normal
    /// 当需要导出使用$$标记公式的md文件时改为：dollar
    /// </summary>
    [JsonPropertyName("formula_mode")]
    public string FormulaMode { get; set; }

    /// <summary>
    /// 导出后的md/tex文件名（不含后缀名），默认output.md/output.tex，仅对md和tex有效；
    /// </summary>
    [JsonPropertyName("filename")]
    public string Filename { get; set; }

    /// <summary>
    /// 合并跨页表格
    /// </summary>
    [JsonPropertyName("merge_cross_page_forms")]
    public bool? MergeCrossPageForms { get; set; }
}
namespace MoAI.Infra.Doc2x.Models;

/// <summary>
/// 导出结果响应数据
/// </summary>
public class Doc2xExportResultResponse
{
    public string Code { get; set; }
    public Doc2xExportData Data { get; set; }
}
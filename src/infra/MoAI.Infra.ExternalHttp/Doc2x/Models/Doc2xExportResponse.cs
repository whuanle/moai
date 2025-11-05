namespace MoAI.Infra.Doc2x.Models;

/// <summary>
/// 导出响应数据
/// </summary>
public class Doc2xExportResponse
{
    public string Code { get; set; }
    public Doc2xExportData Data { get; set; }
}

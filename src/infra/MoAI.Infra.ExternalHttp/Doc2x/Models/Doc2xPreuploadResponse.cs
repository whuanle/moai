namespace MoAI.Infra.Doc2x.Models;

/// <summary>
/// 文件预上传响应数据
/// </summary>
public class Doc2xPreuploadResponse
{
    public string Code { get; set; }
    public Doc2xPreuploadData Data { get; set; }
}
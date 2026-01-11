namespace MoAI.Wiki.Plugins.Paddleocr.Models;

/// <summary>
/// 飞桨 OCR 插件信息.
/// </summary>
public class PaddleocrPluginItem
{
    /// <summary>
    /// 插件 id.
    /// </summary>
    public int PluginId { get; init; }

    /// <summary>
    /// 插件名称.
    /// </summary>
    public string PluginName { get; init; } = default!;

    /// <summary>
    /// 插件标题.
    /// </summary>
    public string Title { get; init; } = default!;

    /// <summary>
    /// 插件描述.
    /// </summary>
    public string Description { get; init; } = default!;

    /// <summary>
    /// 模板 key (paddleocr_ocr, paddleocr_vl, paddleocr_structure_v3).
    /// </summary>
    public string TemplatePluginKey { get; init; } = default!;
}

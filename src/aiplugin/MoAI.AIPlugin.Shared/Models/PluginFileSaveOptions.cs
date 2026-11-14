namespace MoAI.AIPlugin.Models;

/// <summary>
/// 插件文件保存选项.
/// </summary>
public class PluginFileSaveOptions
{
    /// <summary>
    /// 文件名称（只取文件名部分，用于推断扩展名与下载文件名）；留空时使用 <c>file</c>，
    /// 公网文件转存时留空会从响应头或地址自动识别.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// 内容类型（MIME）；留空时按 <see cref="FileName"/> 的扩展名推断.
    /// </summary>
    public string? ContentType { get; set; }

    /// <summary>
    /// 是否保存到公开目录.
    /// <para>
    /// 公开文件可通过 <c>/static</c> 免登录访问，适合图片等可直接在对话中展示的产物；
    /// 包含敏感内容的导出文件应保持私有，通过文件 id 获取预签名下载地址.
    /// </para>
    /// </summary>
    public bool IsPublic { get; set; }

    /// <summary>
    /// 存储子目录前缀；留空时使用 <c>plugins</c>.
    /// <para>
    /// 公开文件实际存储为 <c>public/{Prefix}/...</c>，私有文件存储为 <c>{Prefix}/...</c>.
    /// </para>
    /// </summary>
    public string? Prefix { get; set; }
}

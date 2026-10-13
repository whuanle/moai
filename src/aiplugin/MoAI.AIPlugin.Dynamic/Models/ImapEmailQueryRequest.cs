using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// IMAP 邮箱读取插件请求参数.
/// </summary>
public class ImapEmailQueryRequest
{
    /// <summary>
    /// 操作模式.
    /// </summary>
    [Description("操作模式：recent=最近邮件（默认）/ search=按条件搜索 / detail=读取单封完整内容 / mark_read=标记已读或未读 / folders=列出所有文件夹；未知值会被拒绝")]
    public string Mode { get; set; } = "recent";

    /// <summary>
    /// 列表条数上限.
    /// </summary>
    [Description("recent/search 模式：最多返回条数，1-50（默认 10），按时间新→旧排列")]
    public int Count { get; set; } = 10;

    /// <summary>
    /// 目标文件夹（临时覆盖配置）.
    /// </summary>
    [Description("目标文件夹名（可选），如 INBOX、Archive；留空取实例配置的 Folder")]
    public string? Folder { get; set; }

    /// <summary>
    /// search 模式：发件人包含关键字.
    /// </summary>
    [Description("search 模式：发件人地址包含的关键字（可空）")]
    public string? FromContains { get; set; }

    /// <summary>
    /// search 模式：主题包含关键字.
    /// </summary>
    [Description("search 模式：主题包含的关键字（可空），支持中文（服务器按 IMAP 字面量下发）")]
    public string? SubjectContains { get; set; }

    /// <summary>
    /// search 模式：起始日期.
    /// </summary>
    [Description("search 模式：只看该日期（含）之后的邮件，格式 yyyy-MM-dd（可空）")]
    public string? SinceDate { get; set; }

    /// <summary>
    /// search 模式：仅未读.
    /// </summary>
    [Description("search 模式：是否只看未读邮件（默认 false）")]
    public bool UnreadOnly { get; set; }

    /// <summary>
    /// detail/mark_read 模式：目标邮件 UID.
    /// </summary>
    [Description("detail/mark_read 模式：目标邮件的 UID（必填，取列表里返回的 Uid）")]
    public long Uid { get; set; }

    /// <summary>
    /// mark_read 模式：是否标为未读.
    /// </summary>
    [Description("mark_read 模式：false（默认）=标记为已读，true=标记为未读")]
    public bool Unread { get; set; }
}

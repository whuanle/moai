using System;
using MoAI.Database.Audits;

#pragma warning disable CA1051
#pragma warning disable SA1401
#pragma warning disable SA1600
#pragma warning disable SA1601
#pragma warning disable SA1204
namespace MoAI.Database.Entities;

/// <summary>
/// 通知中心模板：文本 + 变量定义，发送时插值生成通知内容；可携带阿里云/腾讯云短信平台已审核模板映射.
/// </summary>
public partial class NotificationTemplateEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 内置模板标识 code（如 register_email_code，非空表示平台内置模板：不可删除、可编辑，业务按 code 回退解析）.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// 模板名称（唯一）.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 通知主题模板（邮件主题用，短信无主题）.
    /// </summary>
    public string Title { get; set; } = default!;

    /// <summary>
    /// 通知正文模板（纯文本或 HTML，含 {{变量}} 双花括号占位符）.
    /// </summary>
    public string Content { get; set; } = default!;

    /// <summary>
    /// 内容类型.
    /// </summary>
    public int ContentType { get; set; }

    /// <summary>
    /// 变量定义 JSON（[{"name","description","example"}]，发送时按 name 提供变量值）.
    /// </summary>
    public string Variables { get; set; } = default!;

    /// <summary>
    /// 阿里云短信模板 Code（平台审核通过后回填，空表示不用于阿里云短信）.
    /// </summary>
    public string AliyunTemplateCode { get; set; } = default!;

    /// <summary>
    /// 腾讯云短信模板 Id（平台审核通过后回填，空表示不用于腾讯云短信）.
    /// </summary>
    public string TencentTemplateId { get; set; } = default!;

    /// <summary>
    /// 模板描述.
    /// </summary>
    public string Description { get; set; } = default!;

    /// <summary>
    /// 创建人.
    /// </summary>
    public long CreateUserId { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>
    /// 更新人.
    /// </summary>
    public long UpdateUserId { get; set; }

    /// <summary>
    /// 更新时间.
    /// </summary>
    public DateTimeOffset UpdateTime { get; set; }

    /// <summary>
    /// 软删除.
    /// </summary>
    public long IsDeleted { get; set; }
}

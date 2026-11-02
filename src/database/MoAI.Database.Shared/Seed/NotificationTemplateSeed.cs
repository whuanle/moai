using Microsoft.EntityFrameworkCore;
using MoAI.Database.Entities;
using MoAI.Database.Enums;

namespace MoAI.Database.Seed;

/// <summary>
/// 内置通知模板种子数据：注册/登录/找回密码验证码邮件模板（HTML），业务按 code 回退解析，不可删除、可编辑.
/// 存量库补种脚本见 asserts/notification-template.sql（省略 id 列，按 code 判存在）.
/// </summary>
public static class NotificationTemplateSeed
{
    /// <summary>
    /// 注册验证码内置模板 code.
    /// </summary>
    public const string RegisterEmailCode = "register_email_code";

    /// <summary>
    /// 登录验证码内置模板 code.
    /// </summary>
    public const string LoginEmailCode = "login_email_code";

    /// <summary>
    /// 找回密码验证码内置模板 code.
    /// </summary>
    public const string ResetEmailCode = "reset_email_code";

    /// <summary>
    /// 验证码模板支持的变量定义 JSON（code / siteName / expireMinutes）.
    /// </summary>
    private const string CodeVariablesJson =
        """[{"name":"code","description":"验证码","example":"123456"},{"name":"siteName","description":"网站名称","example":"MoAI"},{"name":"expireMinutes","description":"验证码有效分钟数","example":"10"}]""";

    /// <summary>
    /// 应用内置通知模板种子数据.
    /// </summary>
    /// <param name="modelBuilder">模型构建器.</param>
    public static void Apply(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<NotificationTemplateEntity>().HasData(
            new NotificationTemplateEntity
            {
                Id = 1,
                Code = RegisterEmailCode,
                Name = "注册验证码邮件",
                Title = "{{siteName}} 注册验证码",
                Content = BuildContent("你正在注册 {{siteName}}，请使用以下验证码完成注册"),
                ContentType = (int)NotificationContentType.Html,
                Variables = CodeVariablesJson,
                AliyunTemplateCode = string.Empty,
                TencentTemplateId = string.Empty,
                Description = "平台内置模板：注册验证码邮件，系统设置未绑定自定义模板时自动使用；可编辑内容，不可删除。",
                CreateUserId = 1,
                UpdateUserId = 1,
                IsDeleted = 0
            },
            new NotificationTemplateEntity
            {
                Id = 2,
                Code = LoginEmailCode,
                Name = "登录验证码邮件",
                Title = "{{siteName}} 登录验证码",
                Content = BuildContent("你正在登录 {{siteName}}，请使用以下验证码完成登录"),
                ContentType = (int)NotificationContentType.Html,
                Variables = CodeVariablesJson,
                AliyunTemplateCode = string.Empty,
                TencentTemplateId = string.Empty,
                Description = "平台内置模板：登录验证码邮件，系统设置未绑定自定义模板时自动使用；可编辑内容，不可删除。",
                CreateUserId = 1,
                UpdateUserId = 1,
                IsDeleted = 0
            },
            new NotificationTemplateEntity
            {
                Id = 3,
                Code = ResetEmailCode,
                Name = "找回密码验证码邮件",
                Title = "{{siteName}} 找回密码验证码",
                Content = BuildContent("你正在找回 {{siteName}} 账号密码，请使用以下验证码完成找回"),
                ContentType = (int)NotificationContentType.Html,
                Variables = CodeVariablesJson,
                AliyunTemplateCode = string.Empty,
                TencentTemplateId = string.Empty,
                Description = "平台内置模板：找回密码验证码邮件，系统设置未绑定自定义模板时自动使用；可编辑内容，不可删除。",
                CreateUserId = 1,
                UpdateUserId = 1,
                IsDeleted = 0
            });
    }

    /// <summary>
    /// 生成验证码邮件 HTML 正文（内联样式，兼容主流邮件客户端；{{变量}} 双花括号占位符与云邮件平台提审形态一致）.
    /// </summary>
    /// <param name="leadSentence">引导语（含 {{siteName}} 占位符）.</param>
    private static string BuildContent(string leadSentence)
    {
        const string leadToken = "__LEAD__";
        const string html = """
            <div style="margin:0;padding:24px;background-color:#f5f6f8;font-family:'PingFang SC','Microsoft YaHei','Segoe UI',Arial,sans-serif;">
              <div style="max-width:520px;margin:0 auto;background-color:#ffffff;border:1px solid #e5e6eb;border-radius:12px;padding:32px;">
                <h2 style="margin:0 0 16px;font-size:18px;color:#1f2329;">{{siteName}} 验证码</h2>
                <p style="margin:0 0 24px;font-size:14px;line-height:22px;color:#4e5969;">__LEAD__，验证码 <b style="color:#1f2329;">{{expireMinutes}}</b> 分钟内有效。</p>
                <div style="padding:16px 24px;background-color:#f5f6f8;border-radius:8px;text-align:center;font-size:30px;font-weight:bold;letter-spacing:10px;color:#4f46e5;">{{code}}</div>
                <p style="margin:24px 0 0;font-size:12px;line-height:20px;color:#86909c;">如非本人操作，请忽略本邮件；请勿将验证码透露给他人。</p>
              </div>
            </div>
            """;
        return html.Replace(leadToken, leadSentence);
    }
}

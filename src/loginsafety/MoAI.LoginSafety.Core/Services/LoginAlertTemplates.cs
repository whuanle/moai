using MoAI.LoginSafety.Models;

namespace MoAI.LoginSafety.Services;

/// <summary>
/// 登录安全内置通知模板（代码内置，界面不可定制）：中/高风险站内信提醒 + 高风险渠道告警.
/// <para>zh-CN/en-US 双语，默认中文；插值基于 <see cref="LoginAlertTemplate.Render"/> 白名单，
/// 占位符 <c>{userName} {ip} {location} {time} {siteName} {riskLevel} {reasons}</c>.</para>
/// </summary>
public static class LoginAlertTemplates
{
    /// <summary>默认语言（异步任务无请求上下文，固定中文）.</summary>
    public const string DefaultLanguage = "zh-CN";

    /// <summary>
    /// 解析语言：当前无用户语言字段与请求上下文，除 en-US 外一律回落中文.
    /// </summary>
    /// <param name="language">语言标识（预留）.</param>
    /// <returns>规范化语言标识.</returns>
    public static string ResolveLanguage(string? language)
    {
        return string.Equals(language, "en-US", StringComparison.OrdinalIgnoreCase) ? "en-US" : DefaultLanguage;
    }

    /// <summary>
    /// 风险等级显示文案.
    /// </summary>
    /// <param name="level">风险等级.</param>
    /// <param name="language">语言标识.</param>
    /// <returns>返回等级文案.</returns>
    public static string RiskLevelText(LoginRiskLevel level, string language)
    {
        return language == "en-US"
            ? level switch
            {
                LoginRiskLevel.High => "High",
                LoginRiskLevel.Medium => "Medium",
                LoginRiskLevel.Low => "Low",
                _ => "None"
            }
            : level switch
            {
                LoginRiskLevel.High => "高",
                LoginRiskLevel.Medium => "中",
                LoginRiskLevel.Low => "低",
                _ => "无"
            };
    }

    /// <summary>
    /// 渲染风险登录站内提醒（发给受影响用户本人，中/高风险登录成功）.
    /// </summary>
    /// <param name="language">语言标识.</param>
    /// <param name="values">变量表（userName/ip/location/time/siteName）.</param>
    /// <returns>返回标题与正文.</returns>
    public static (string Title, string Content) UserAlert(string language, IReadOnlyDictionary<string, string> values)
    {
        if (language == "en-US")
        {
            return (
                LoginAlertTemplate.Render("{siteName} login security alert", values),
                LoginAlertTemplate.Render("Hello {userName}:\n\nYour account was successfully signed in from a new environment at {time}.\n\nIP: {ip}\nLocation: {location}\n\nIf this was not you, please change your password immediately and contact your administrator.", values));
        }

        return (
            LoginAlertTemplate.Render("{siteName} 登录安全提醒", values),
            LoginAlertTemplate.Render("{userName}，你好：\n\n你的账号于 {time} 在新环境登录成功。\n\n登录 IP：{ip}\n登录地点：{location}\n\n如非本人操作，请立即修改密码并联系管理员。", values));
    }

    /// <summary>
    /// 渲染高风险事件告警（推送给通知渠道，管理员侧消费；选 SMTP 渠道时即告警邮件）.
    /// </summary>
    /// <param name="language">语言标识.</param>
    /// <param name="values">变量表（userName/ip/location/riskLevel/reasons/time/siteName）.</param>
    /// <returns>返回标题与正文.</returns>
    public static (string Title, string Content) HighRiskAlert(string language, IReadOnlyDictionary<string, string> values)
    {
        if (language == "en-US")
        {
            return (
                LoginAlertTemplate.Render("Login risk alert: {userName}", values),
                LoginAlertTemplate.Render("High-risk login behavior detected:\n\nUser: {userName}\nRisk level: {riskLevel}\nRules: {reasons}\nIP: {ip}\nLocation: {location}\nTime: {time}\n\nPlease review the details in the admin console under Login Events.", values));
        }

        // 标题不带站点名：SMTP 渠道自带【MoAI】主题前缀，机器人渠道发到专属会话，避免双前缀
        return (
            LoginAlertTemplate.Render("登录风险告警：{userName}", values),
            LoginAlertTemplate.Render("检测到高风险登录行为：\n\n用户：{userName}\n风险等级：{riskLevel}\n命中规则：{reasons}\n登录 IP：{ip}\n登录地点：{location}\n时间：{time}\n\n请在管理后台「日志事件」中查看详情并处置。", values));
    }
}

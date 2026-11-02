using MoAI.Notification.Models;

namespace MoAI.Notification.Services;

/// <summary>
/// 通知模板渲染服务：供业务模块把绑定/内置的 notification_template 模板渲染为最终标题/正文
/// （如验证码邮件模板），复用模板应用的变量校验与渲染语义，不发送.
/// </summary>
public interface INotificationTemplateRenderService
{
    /// <summary>
    /// 按模板 id 渲染：模板不存在抛 404，声明的变量缺少值抛 400（列出缺失变量名）.
    /// </summary>
    /// <param name="templateId">通知模板 id.</param>
    /// <param name="values">模板变量值表，与模板变量定义按 name 对应.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回渲染后的最终标题/正文.</returns>
    Task<RenderedNotificationTemplate> RenderByIdAsync(long templateId, IReadOnlyDictionary<string, string>? values, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按内置模板 code 渲染（内置模板随种子数据提供，不可删除）：code 无对应模板返回 null，由调用方回退默认模板.
    /// </summary>
    /// <param name="builtinCode">内置模板标识 code（如 register_email_code）.</param>
    /// <param name="values">模板变量值表，与模板变量定义按 name 对应.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回渲染后的最终标题/正文，无对应内置模板时返回 null.</returns>
    Task<RenderedNotificationTemplate?> RenderByBuiltinCodeAsync(string builtinCode, IReadOnlyDictionary<string, string>? values, CancellationToken cancellationToken = default);
}

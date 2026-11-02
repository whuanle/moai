using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Maomi;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Notification.Models;

namespace MoAI.Notification.Services;

/// <summary>
/// 通知模板应用器：把携带 <see cref="NotificationMessage.TemplateId"/> 的消息解析渲染为最终文本，
/// 填充短信平台模板映射；无模板的消息原样返回（直发行为不变）.
/// <para>渲染一次完成并清空 TemplateId，返回的消息即为最终形态：领域服务与广播/试发 Handler 共用，
/// 幂等（重复应用为无操作），Hangfire 队列中的消息不再依赖模板存续.</para>
/// </summary>
[InjectOnScoped]
public class NotificationTemplateApplier
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationTemplateApplier"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public NotificationTemplateApplier(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <summary>
    /// 应用模板：模板存在时校验变量值完整性并渲染标题/正文，填充阿里云/腾讯云模板映射与参数顺序；
    /// 模板不存在抛 404，变量缺失抛 400（列出缺失变量名）.
    /// </summary>
    /// <param name="message">原始消息.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回渲染后的最终消息（无模板时原样返回）.</returns>
    public async Task<NotificationMessage> ApplyAsync(NotificationMessage message, CancellationToken cancellationToken = default)
    {
        if (message.TemplateId is not > 0)
        {
            return message;
        }

        var entity = await _databaseContext.NotificationTemplates.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == message.TemplateId.Value, cancellationToken)
            ?? throw new BusinessException("通知模板不存在") { StatusCode = 404 };

        var variables = NotificationTemplateVariableParser.Parse(entity.Variables, out _)
            ?? throw new BusinessException("通知模板的变量定义无效") { StatusCode = 400 };

        var missing = NotificationTemplateVariableParser.MissingValues(variables, message.TemplateVariables);
        if (missing.Count > 0)
        {
            throw new BusinessException($"模板变量缺少值：{string.Join("、", missing)}") { StatusCode = 400 };
        }

        var values = message.TemplateVariables ?? new Dictionary<string, string>();
        var renderedTitle = entity.Title.Length > 0
            ? NotificationTemplateRenderer.Format(entity.Title, values)
            : message.Title;
        var renderedContent = NotificationTemplateRenderer.Format(entity.Content, values);

        return new NotificationMessage
        {
            Title = renderedTitle,
            Content = renderedContent,
            Level = message.Level,
            Source = message.Source,
            TargetUserIds = message.TargetUserIds,
            EmailRecipients = message.EmailRecipients,
            PhoneNumbers = message.PhoneNumbers,
            TemplateVariables = message.TemplateVariables,
            ContentType = (NotificationContentType)entity.ContentType,
            SmsAliyunTemplateCode = entity.AliyunTemplateCode.Length > 0 ? entity.AliyunTemplateCode : null,
            SmsTencentTemplateId = entity.TencentTemplateId.Length > 0 ? entity.TencentTemplateId : null,
            SmsTemplateParamNames = NotificationTemplateRenderer.ExtractNames(entity.Content).ToArray()
        };
    }
}

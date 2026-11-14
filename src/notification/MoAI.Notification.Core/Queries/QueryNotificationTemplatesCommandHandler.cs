using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Account.Services;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Notification.Queries;

namespace MoAI.Notification.Queries;

/// <summary>
/// <inheritdoc cref="QueryNotificationTemplatesCommand"/>.
/// </summary>
public class QueryNotificationTemplatesCommandHandler : IRequestHandler<QueryNotificationTemplatesCommand, QueryNotificationTemplatesCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IUserInfoFillService _userInfoFillService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryNotificationTemplatesCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="userInfoFillService">用户信息填充服务.</param>
    public QueryNotificationTemplatesCommandHandler(DatabaseContext databaseContext, IUserInfoFillService userInfoFillService)
    {
        _databaseContext = databaseContext;
        _userInfoFillService = userInfoFillService;
    }

    /// <inheritdoc/>
    public async Task<QueryNotificationTemplatesCommandResponse> Handle(QueryNotificationTemplatesCommand request, CancellationToken cancellationToken)
    {
        var query = _databaseContext.NotificationTemplates.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            query = query.Where(x => x.Name.Contains(request.Search));
        }

        var items = await query
            .OrderBy(x => x.Id)
            .Select(x => new QueryNotificationTemplatesCommandResponseItem
            {
                Id = x.Id,
                IsBuiltIn = x.Code != string.Empty,
                Name = x.Name,
                Title = x.Title,
                Content = x.Content,
                ContentType = (NotificationContentType)x.ContentType,
                Variables = x.Variables,
                AliyunTemplateCode = x.AliyunTemplateCode,
                TencentTemplateId = x.TencentTemplateId,
                Description = x.Description,
                CreateUserId = (int)x.CreateUserId,
                CreateTime = x.CreateTime,
                UpdateUserId = (int)x.UpdateUserId,
                UpdateTime = x.UpdateTime
            })
            .ToListAsync(cancellationToken);

        await _userInfoFillService.FillAsync(items, cancellationToken);
        return new QueryNotificationTemplatesCommandResponse { Items = items };
    }
}

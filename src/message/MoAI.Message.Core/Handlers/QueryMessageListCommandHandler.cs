using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Account.Services;
using MoAI.Database;
using MoAI.Message.Queries;
using MoAI.Message.Queries.Responses;

namespace MoAI.Message.Handlers;

/// <summary>
/// <inheritdoc cref="QueryMessageListCommand"/>.
/// </summary>
public class QueryMessageListCommandHandler : IRequestHandler<QueryMessageListCommand, QueryMessageListCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IUserInfoFillService _userInfoFillService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryMessageListCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="userInfoFillService">用户名填充服务.</param>
    public QueryMessageListCommandHandler(DatabaseContext databaseContext, IUserInfoFillService userInfoFillService)
    {
        _databaseContext = databaseContext;
        _userInfoFillService = userInfoFillService;
    }

    /// <inheritdoc/>
    public async Task<QueryMessageListCommandResponse> Handle(QueryMessageListCommand request, CancellationToken cancellationToken)
    {
        var query = _databaseContext.Messages
            .AsNoTracking()
            .Where(x => x.RecipientUserId == request.ContextUserId);

        if (request.MessageType != null)
        {
            query = query.Where(x => x.MessageType == (int)request.MessageType);
        }

        if (request.IsRead != null)
        {
            query = query.Where(x => x.IsRead == request.IsRead);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            // id 为 identity 自增，倒序即发送时间倒序（避免各库对 DateTimeOffset 排序的兼容问题）
            .OrderByDescending(x => x.Id)
            .Skip((request.PageNo - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => new MessageItem
            {
                Id = x.Id,
                Title = x.Title,
                Content = x.Content,
                MessageType = (Models.MessageType)x.MessageType,
                Source = x.Source,
                SenderUserId = x.SenderUserId,
                Params = x.Params,
                IsRead = x.IsRead,
                ReadTime = x.ReadTime,
                CreateUserId = (int)(x.SenderUserId ?? 0),
                CreateTime = x.CreateTime,
                UpdateUserId = 0,
                UpdateTime = x.UpdateTime,
            })
            .ToListAsync(cancellationToken);

        // 填充发送人名（系统消息 create_user_id=0，填充后为空）
        await _userInfoFillService.FillAsync(items, cancellationToken);

        return new QueryMessageListCommandResponse
        {
            Items = items,
            Total = total,
            PageNo = request.PageNo,
            PageSize = request.PageSize,
        };
    }
}

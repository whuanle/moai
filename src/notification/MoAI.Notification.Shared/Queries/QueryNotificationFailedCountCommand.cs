using MediatR;

namespace MoAI.Notification.Queries;

/// <summary>
/// 查询发送失败的记录数量（前端菜单「通知中心」角标数据源），仅管理员.
/// </summary>
public class QueryNotificationFailedCountCommand : IRequest<QueryNotificationFailedCountCommandResponse>
{
}

/// <summary>
/// 发送失败记录数量响应.
/// </summary>
public class QueryNotificationFailedCountCommandResponse
{
    /// <summary>
    /// 发送失败记录数量.
    /// </summary>
    public int Count { get; set; }
}

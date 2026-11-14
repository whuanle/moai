using MediatR;

namespace MoAI.LoginSafety.Commands;

/// <summary>
/// 查询未读风险事件数量（菜单「日志事件」角标数据源），仅管理员.
/// </summary>
public class QueryRiskEventUnreadCountCommand : IRequest<QueryRiskEventUnreadCountCommandResponse>
{
}

/// <summary>
/// 未读风险事件数量响应.
/// </summary>
public class QueryRiskEventUnreadCountCommandResponse
{
    /// <summary>
    /// 未读风险事件数量.
    /// </summary>
    public int Count { get; set; }
}

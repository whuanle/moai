using MediatR;

namespace MoAI.Publication.Queries;

/// <summary>
/// 查询待审核上架申请数量（前端菜单「审批上架」角标数据源），仅系统管理员.
/// </summary>
public class QueryPublicationPendingCountCommand : IRequest<QueryPublicationPendingCountCommandResponse>
{
}

/// <summary>
/// 待审核上架申请数量响应.
/// </summary>
public class QueryPublicationPendingCountCommandResponse
{
    /// <summary>
    /// 待审核数量.
    /// </summary>
    public int Count { get; set; }
}

using MediatR;
using MoAI.Database.Enums;
using MoAI.Subsystem.Queries.Responses;

namespace MoAI.Subsystem.Queries;

/// <summary>
/// 查询系统应用接入列表（root，分页 + 名称/前缀搜索）.
/// </summary>
public class QuerySystemAccessAppsCommand : IRequest<QuerySystemAccessAppsCommandResponse>
{
    /// <summary>
    /// 页码，从 1 起.
    /// </summary>
    public int PageNo { get; init; } = 1;

    /// <summary>
    /// 每页数量.
    /// </summary>
    public int PageSize { get; init; } = 20;

    /// <summary>
    /// 搜索关键字（名称/key 前缀模糊）.
    /// </summary>
    public string? Search { get; init; }
}

/// <summary>
/// 系统应用接入自省（协作面 /api/subsys/system/info，Bearer key 直连）：
/// 身份字段由协作面控制器从鉴权中间件解析的调用方上下文回填.
/// </summary>
public class QuerySystemAccessAppInfoCommand : IRequest<QuerySystemAccessAppInfoCommandResponse>
{
    /// <summary>
    /// 接入 id（服务端回填，不接受请求体注入）.
    /// </summary>
    public Guid AppId { get; set; }

    /// <summary>
    /// 接入名称（服务端回填）.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 功能范围（服务端回填）.
    /// </summary>
    public SystemApiKeyScopes Scopes { get; set; }

    /// <summary>
    /// 状态（服务端回填）.
    /// </summary>
    public AccessAppStatus Status { get; set; }
}

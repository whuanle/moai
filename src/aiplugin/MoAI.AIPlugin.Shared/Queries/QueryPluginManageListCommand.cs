using MediatR;
using MoAI.AIPlugin.Queries.Responses;

namespace MoAI.AIPlugin.Queries;

/// <summary>
/// 查询插件管理列表（DB 来源，含分类信息）.
/// </summary>
public class QueryPluginManageListCommand : IRequest<QueryPluginManageListCommandResponse>
{
    /// <summary>
    /// 插件种类：custom|dynamic|static，为空则不区分.
    /// </summary>
    public string? Kind { get; init; }
}

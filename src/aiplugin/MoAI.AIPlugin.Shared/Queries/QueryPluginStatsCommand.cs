using MediatR;
using MoAI.AIPlugin.Queries.Responses;

namespace MoAI.AIPlugin.Queries;

/// <summary>
/// 查询插件使用统计（全团队汇总）：概览计数 + 调用量 TOP + 分类分布 + 近 30 天趋势；仅管理员.
/// </summary>
public class QueryPluginStatsCommand : IRequest<QueryPluginStatsCommandResponse>
{
}

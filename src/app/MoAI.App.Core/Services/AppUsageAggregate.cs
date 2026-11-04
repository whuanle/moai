using System.Collections.Generic;
using MoAI.App.Queries.Responses;

namespace MoAI.App.Services;

/// <summary>
/// 应用用量聚合（汇总 + 按模型分布），使用分析的 Redis 缓存单元.
/// </summary>
/// <param name="Summary">用量汇总.</param>
/// <param name="ByModel">按模型分布（按合计 token 倒序）.</param>
public record AppUsageAggregate(AppUsageSummary Summary, IReadOnlyList<AppUsageModelItem> ByModel);

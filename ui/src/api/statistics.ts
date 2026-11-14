import { getApiClient } from '@/api/kiota'

/** 趋势单日数据点. */
export interface UsageTrendPoint {
  /** 统计日期（yyyy-MM-dd，UTC）. */
  date: string
  /** 当日合计（按小时粒度落库的已聚合到日）. */
  count: number
}

/** 资源使用趋势响应（区间内无数据的日期补 0）. */
export interface UsageTrend {
  items: UsageTrendPoint[]
  total: number
  startDate: string
  endDate: string
}

/**
 * 按日聚合查询资源使用趋势，仅管理员.
 * @param name 资源类型名（如 plugin-usage），空则统计全部类型合计
 * @param days 区间天数（含今天 UTC），默认 30
 */
export async function getUsageTrend(name?: string, days = 30): Promise<UsageTrend> {
  const client = getApiClient()
  const end = new Date()
  const start = new Date(end.getTime() - (days - 1) * 86_400_000)
  const fmt = (d: Date) => d.toISOString().slice(0, 10)
  const res = await client.api.statistics.usage.trend.get({
    queryParameters: {
      name,
      // Kiota DateOnly 参数接受 ISO 字符串形态的 year-month-day
      startDate: fmt(start) as never,
      endDate: fmt(end) as never,
    },
  })
  return {
    items: (res?.items ?? []).map((i) => ({ date: String(i.date ?? ''), count: Number(i.count ?? 0) })),
    total: Number(res?.total ?? 0),
    startDate: String(res?.startDate ?? ''),
    endDate: String(res?.endDate ?? ''),
  }
}

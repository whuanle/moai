import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import '@/i18n'
import { AppMonitorSection } from '../AppMonitorSection'
import { getAppUsage, getAppUsageTrend } from '@/api/app'
import { DualAxes } from '@antv/gpt-vis'

vi.mock('@/api/app', () => ({
  getAppUsage: vi.fn(),
  getAppUsageTrend: vi.fn(),
}))

vi.mock('@/api/memory', () => ({
  getAppMemoryStats: vi.fn().mockResolvedValue({ totalMemories: 5, userCount: 2 }),
}))

// gpt-vis 在 jsdom 下无法真正建 canvas，mock 实例捕获 render 配置供断言
vi.mock('@antv/gpt-vis', () => ({
  DualAxes: vi.fn(() => ({ render: vi.fn(), destroy: vi.fn() })),
}))

const USAGE_STATS = { sessionCount: 4, userCount: 2, messageCount: 30, lastActiveTime: '2026-10-12T10:00:00+08:00' }

const TREND_DATA = {
  items: Array.from({ length: 30 }, (_, i) => ({
    date: `2026-10-${String(i + 1).padStart(2, '0')}`,
    useCount: i === 0 ? 3 : 0,
    messageCount: i === 0 ? 9 : 0,
  })),
  startDate: '2026-10-01',
  endDate: '2026-10-30',
}

describe('AppMonitorSection（应用使用分析：浅色指标卡 + 模型芯片 + 趋势）', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(getAppUsageTrend).mockResolvedValue(TREND_DATA as Awaited<ReturnType<typeof getAppUsageTrend>>)
  })

  it('Tokens 四卡 + 活动指标卡 + 模型芯片卡带占比与明细', async () => {
    vi.mocked(getAppUsage).mockResolvedValue({
      summary: { callCount: 3, promptTokens: 400, completionTokens: 600, totalTokens: 1000, cachedTokens: 60 },
      byModel: [
        {
          modelId: 'm1',
          modelName: 'gpt-4o',
          callCount: 3,
          promptTokens: 400,
          completionTokens: 600,
          totalTokens: 1000,
          cachedTokens: 60,
        },
      ],
      stats: USAGE_STATS,
    } as unknown as Awaited<ReturnType<typeof getAppUsage>>)

    render(<AppMonitorSection appId="a1" />)

    expect(await screen.findByText('gpt-4o')).toBeTruthy()
    // Tokens 指标卡：合计大数 / 输入（缓存命中副行）/ 缓存命中率环（60/400 ≈ 15%）
    expect(screen.getAllByText('1,000').length).toBeGreaterThan(0)
    expect(screen.getByText('合计 Tokens')).toBeTruthy()
    expect(screen.getByText('输入 Tokens')).toBeTruthy()
    expect(screen.getByText('输出 Tokens')).toBeTruthy()
    expect(screen.getAllByText('15%').length).toBeGreaterThanOrEqual(1)
    expect(screen.getByText('缓存命中率')).toBeTruthy()
    // 活动统计与记忆条数指标卡
    expect(screen.getByText('使用用户数')).toBeTruthy()
    expect(screen.getByText('2')).toBeTruthy()
    expect(screen.getByText('记忆条数')).toBeTruthy()
    expect(screen.getByText('5')).toBeTruthy()
    // 模型芯片卡：占比徽标 + 调用明细元信息行（无表格）
    expect(screen.getByText('100%')).toBeTruthy()
    expect(screen.getByText('调用 3 次 · 输入 400 · 输出 600 · 缓存命中 60')).toBeTruthy()
    await waitFor(() => expect(getAppUsage).toHaveBeenCalledWith('a1'))
  })

  it('趋势卡渲染近 30 天双轴图并传入使用次数与消息条数序列', async () => {
    const renderMock = vi.fn()
    vi.mocked(DualAxes).mockImplementation(() => ({ render: renderMock, destroy: vi.fn() }) as never)
    vi.mocked(getAppUsage).mockResolvedValue({
      summary: {},
      byModel: [],
      stats: USAGE_STATS,
    } as unknown as Awaited<ReturnType<typeof getAppUsage>>)

    render(<AppMonitorSection appId="a1" />)

    expect(await screen.findByText('近 30 天趋势')).toBeTruthy()
    await waitFor(() => expect(getAppUsageTrend).toHaveBeenCalledWith('a1', 30))
    // DualAxes({ container, ... }) 实例化后 render 接收柱（使用次数）+ 线（消息条数）双序列
    await waitFor(() => expect(renderMock).toHaveBeenCalled())
    const cfg = renderMock.mock.calls[0]?.[0] as {
      categories: string[]
      series: { type: string; data: number[]; axisYTitle: string }[]
    }
    expect(cfg.categories.length).toBe(30)
    expect(cfg.categories[0]).toBe('10-01')
    expect(cfg.series).toHaveLength(2)
    expect(cfg.series[0].type).toBe('column')
    expect(cfg.series[0].data[0]).toBe(3)
    expect(cfg.series[0].axisYTitle).toBe('使用次数')
    expect(cfg.series[1].type).toBe('line')
    expect(cfg.series[1].data[0]).toBe(9)
    expect(cfg.series[1].axisYTitle).toBe('消息条数')
  })

  it('趋势全为 0 时展示空态', async () => {
    vi.mocked(getAppUsage).mockResolvedValue({
      summary: {},
      byModel: [],
      stats: USAGE_STATS,
    } as unknown as Awaited<ReturnType<typeof getAppUsage>>)
    vi.mocked(getAppUsageTrend).mockResolvedValue({
      items: [{ date: '2026-10-01', useCount: 0, messageCount: 0 }],
      startDate: '2026-10-01',
      endDate: '2026-10-01',
    } as Awaited<ReturnType<typeof getAppUsageTrend>>)

    render(<AppMonitorSection appId="a1" />)

    expect(await screen.findByText('暂无趋势数据')).toBeTruthy()
  })

  it('空用量数据时不崩溃并展示空态', async () => {
    vi.mocked(getAppUsage).mockResolvedValue({ summary: {}, byModel: [], stats: USAGE_STATS } as unknown as Awaited<
      ReturnType<typeof getAppUsage>
    >)
    vi.mocked(getAppUsageTrend).mockResolvedValue({ items: [], startDate: '', endDate: '' } as Awaited<
      ReturnType<typeof getAppUsageTrend>
    >)

    render(<AppMonitorSection appId="a1" />)

    expect(await screen.findByText('暂无用量数据')).toBeTruthy()
    expect(screen.getAllByText('0').length).toBeGreaterThanOrEqual(4)
  })
})

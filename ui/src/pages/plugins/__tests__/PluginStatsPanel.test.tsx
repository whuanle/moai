import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import { PluginStatsPanel } from '../PluginStatsPanel'
import { pluginApi, type PluginStatsData } from '@/api/plugin'

vi.mock('@/api/plugin', () => ({
  pluginApi: {
    getPluginStats: vi.fn(),
  },
}))

const STATS: PluginStatsData = {
  totalPlugins: 3,
  mcpCount: 1,
  openApiCount: 1,
  totalCalls: 43,
  totalFunctions: 8,
  top: [
    { id: '2', name: '插件B', teamId: 7, teamName: '研发部', counter: 30, avatarPath: null },
    { id: '1', name: '插件A', teamId: 0, teamName: null, counter: 12, avatarPath: null },
  ],
  classifies: [
    { classifyId: 1, classifyName: '工具', count: 2 },
    { classifyId: 0, classifyName: null, count: 1 },
  ],
  trend: {
    items: [{ date: '2026-11-01', count: 0 }],
    total: 0,
    startDate: '2026-11-01',
    endDate: '2026-11-01',
  },
}

describe('PluginStatsPanel', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(pluginApi.getPluginStats).mockResolvedValue(STATS)
  })

  it('概览卡取自 stats 端点（全团队汇总：总数/累计调用/函数总数）', async () => {
    render(<PluginStatsPanel />)

    expect(await screen.findByText('43')).toBeInTheDocument()
    expect(screen.getByText('3')).toBeInTheDocument()
    // 函数总数渲染在类型构成卡的「共 N 个函数」文案中
    expect(screen.getByText('共 8 个函数')).toBeInTheDocument()
  })

  it('TOP 名称：团队自有插件附加团队名 {名称}-({团队名})，系统侧插件只显示名称', async () => {
    render(<PluginStatsPanel />)

    expect(await screen.findByText('插件B-(研发部)')).toBeInTheDocument()
    expect(screen.getByText('插件A')).toBeInTheDocument()
  })

  it('分类分布直接使用后端 classifyName，未分类兜底文案', async () => {
    render(<PluginStatsPanel />)

    expect(await screen.findByText('工具')).toBeInTheDocument()
    expect(screen.getByText('未分类')).toBeInTheDocument()
  })

  it('调用趋势：有数据时按日渲染柱状并展示区间合计', async () => {
    vi.mocked(pluginApi.getPluginStats).mockResolvedValue({
      ...STATS,
      trend: {
        items: [
          { date: '2026-11-01', count: 3 },
          { date: '2026-11-02', count: 0 },
        ],
        total: 3,
        startDate: '2026-11-01',
        endDate: '2026-11-02',
      },
    })
    render(<PluginStatsPanel />)

    expect(await screen.findByText('近 2 天合计 3 次')).toBeInTheDocument()
    expect(screen.getByTitle('2026-11-01：3')).toBeInTheDocument()
  })

  it('调用趋势：近 30 天无调用展示空态', async () => {
    render(<PluginStatsPanel />)

    expect(await screen.findByText(/暂无插件调用/)).toBeInTheDocument()
  })
})

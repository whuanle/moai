import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import { PluginStatsPanel } from '../PluginStatsPanel'
import { pluginApi } from '@/api/plugin'
import { getUsageTrend } from '@/api/statistics'

vi.mock('@/api/plugin', () => ({
  pluginApi: {
    getManagePlugins: vi.fn(),
    getDynamicTemplates: vi.fn(),
    saveDynamicPlugin: vi.fn(),
    deleteDynamicPlugin: vi.fn(),
    runPlugin: vi.fn(),
  },
}))

vi.mock('@/api/statistics', () => ({
  getUsageTrend: vi.fn(),
}))

vi.mock('@/api/classify', () => ({
  classifyApi: {
    getClassifies: vi.fn().mockResolvedValue([]),
    getPluginClassifies: vi.fn().mockResolvedValue([]),
  },
  classifyLabel: (c: { emoji?: string | null; name?: string | null }) => [c?.emoji, c?.name].filter(Boolean).join(' '),
  ClassifyType: { Plugin: 'plugin', App: 'app', Kb: 'kb', Prompt: 'prompt', Skill: 'skill' },
}))

const ITEMS = [
  { id: '1', pluginName: 'a', title: '插件A', type: 0, classifyId: 1, counter: '12', functionCount: 5 },
  { id: '2', pluginName: 'b', title: '插件B', type: 1, classifyId: 0, counter: '30', functionCount: 2 },
  { id: '3', pluginName: 'c', title: '插件C', type: 2, classifyId: 1, counter: '1', functionCount: 1 },
]

describe('PluginStatsPanel', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(pluginApi.getManagePlugins).mockResolvedValue(ITEMS as never)
    vi.mocked(getUsageTrend).mockResolvedValue({
      items: [{ date: '2026-11-01', count: 0 }],
      total: 0,
      startDate: '2026-11-01',
      endDate: '2026-11-01',
    })
  })

  it('渲染概览卡：总数/累计调用/函数总数取自管理列表聚合', async () => {
    render(<PluginStatsPanel classifies={[{ classifyId: 1, name: '工具', emoji: '🛠' }]} />)

    // 累计调用 = Σcounter = 12+30+1，数据到达后才渲染
    expect(await screen.findByText('43')).toBeInTheDocument()
    expect(screen.getByText('3')).toBeInTheDocument()
    // 函数总数渲染在类型构成卡的「共 N 个函数」文案中
    expect(screen.getByText('共 8 个函数')).toBeInTheDocument()
  })

  it('TOP5 含累计调用最高者，分类分布区分已分类与未分类', async () => {
    render(<PluginStatsPanel classifies={[{ classifyId: 1, name: '工具', emoji: '🛠' }]} />)

    expect(await screen.findByText('插件B')).toBeInTheDocument()
    expect(screen.getByText('调用量 TOP 5')).toBeInTheDocument()
    expect(screen.getByText('🛠 工具')).toBeInTheDocument()
    expect(screen.getByText('未分类')).toBeInTheDocument()
  })

  it('调用趋势：有数据时按日渲染柱状并展示区间合计', async () => {
    vi.mocked(getUsageTrend).mockResolvedValue({
      items: [
        { date: '2026-11-01', count: 3 },
        { date: '2026-11-02', count: 0 },
      ],
      total: 3,
      startDate: '2026-11-01',
      endDate: '2026-11-02',
    })
    render(<PluginStatsPanel classifies={[]} />)

    expect(await screen.findByText('近 2 天合计 3 次')).toBeInTheDocument()
    expect(screen.getByTitle('2026-11-01：3')).toBeInTheDocument()
  })

  it('调用趋势：近 30 天无调用展示空态', async () => {
    vi.mocked(getUsageTrend).mockResolvedValue({
      items: [{ date: '2026-11-01', count: 0 }],
      total: 0,
      startDate: '2026-11-01',
      endDate: '2026-11-01',
    })
    render(<PluginStatsPanel classifies={[]} />)

    expect(await screen.findByText(/暂无插件调用/)).toBeInTheDocument()
  })
})

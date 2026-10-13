import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import '@/i18n'
import { AppMonitorSection } from '../AppMonitorSection'
import { getAppUsage } from '@/api/app'

vi.mock('@/api/app', () => ({
  getAppUsage: vi.fn(),
}))

vi.mock('@/api/memory', () => ({
  getAppMemoryStats: vi.fn().mockResolvedValue({ totalMemories: 5, userCount: 2 }),
}))

const USAGE_STATS = { sessionCount: 4, userCount: 2, messageCount: 30, lastActiveTime: '2026-10-12T10:00:00+08:00' }

describe('AppMonitorSection（应用用量监控）', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('渲染汇总数字与按模型分布行', async () => {
    vi.mocked(getAppUsage).mockResolvedValue({
      summary: { callCount: '3', promptTokens: '400', completionTokens: '600', totalTokens: '1000' },
      byModel: [
        {
          modelId: 'm1',
          modelName: 'gpt-4o',
          callCount: '3',
          promptTokens: '400',
          completionTokens: '600',
          totalTokens: '1000',
        },
      ],
      stats: USAGE_STATS,
    } as unknown as Awaited<ReturnType<typeof getAppUsage>>)

    render(<AppMonitorSection appId="a1" />)

    expect(await screen.findByText('gpt-4o')).toBeTruthy()
    expect(screen.getAllByText('3').length).toBeGreaterThan(0)
    expect(screen.getAllByText('1,000').length).toBeGreaterThan(0)
    // 活动统计与记忆条数
    expect(screen.getByText('使用用户数')).toBeTruthy()
    expect(screen.getByText('2')).toBeTruthy()
    expect(screen.getByText('记忆条数')).toBeTruthy()
    expect(screen.getByText('5')).toBeTruthy()
    await waitFor(() => expect(getAppUsage).toHaveBeenCalledWith('a1'))
  })

  it('展示聚合用量滞后提示', async () => {
    vi.mocked(getAppUsage).mockResolvedValue({ summary: {}, byModel: [], stats: USAGE_STATS } as unknown as Awaited<
      ReturnType<typeof getAppUsage>
    >)

    render(<AppMonitorSection appId="a1" />)

    expect(
      await screen.findByText(
        '数据来自聚合用量表，最多滞后约 1 分钟；不含调试会话；趋势图将在后续版本提供。',
      ),
    ).toBeTruthy()
  })

  it('空用量数据时不崩溃并展示空态', async () => {
    vi.mocked(getAppUsage).mockResolvedValue({ summary: {}, byModel: [], stats: USAGE_STATS } as unknown as Awaited<
      ReturnType<typeof getAppUsage>
    >)

    render(<AppMonitorSection appId="a1" />)

    expect(await screen.findByText('暂无用量数据')).toBeTruthy()
    expect(screen.getAllByText('0').length).toBeGreaterThanOrEqual(4)
  })
})

import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import '@/i18n'
import { TeamTimers } from '../TeamTimers'
import { getTeamTimers, createTeamTimer, runTeamTimer, getTeamTimerExecutions } from '@/api/timer'
import { getApps } from '@/api/app'

vi.mock('@/api/timer', () => ({
  getTeamTimers: vi.fn().mockResolvedValue({ totalCount: 0, items: [] }),
  createTeamTimer: vi.fn().mockResolvedValue('t-1'),
  updateTeamTimer: vi.fn().mockResolvedValue(undefined),
  deleteTeamTimer: vi.fn().mockResolvedValue(undefined),
  runTeamTimer: vi.fn().mockResolvedValue('e-1'),
  getTeamTimerExecutions: vi.fn().mockResolvedValue({ totalCount: 0, items: [] }),
  isTimerStatusActive: (status?: string | null) =>
    status === 'pending' || status === 'running' || status === 'retrying',
}))

vi.mock('@/api/app', () => ({
  getApps: vi.fn().mockResolvedValue({ teamId: '7', myRole: 1, items: [] }),
}))

vi.mock('@/api/kiota', () => ({
  getApiClient: vi.fn(() => ({})),
}))

function renderTimers(canManage = true) {
  return render(<TeamTimers teamId={7} canManage={canManage} />)
}

describe('TeamTimers', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(getTeamTimers).mockResolvedValue({ totalCount: 0, items: [] })
    vi.mocked(getTeamTimerExecutions).mockResolvedValue({ totalCount: 0, items: [] })
    vi.mocked(getApps).mockResolvedValue({ teamId: '7', myRole: 1, items: [] })
  })

  it('Admin 可见新建按钮与操作列，加载定时器列表', async () => {
    vi.mocked(getTeamTimers).mockResolvedValue({
      totalCount: 1,
      items: [
        {
          timerId: 't-1',
          teamId: '7',
          name: '每日报告',
          description: '发送日报',
          appId: 'a-1',
          appName: '销售助手',
          appType: 'agent',
          appPublishStatus: 1,
          isEnabled: true,
          cronExpression: '0 1 * * *',
          inputContent: '生成今日日报',
          maxRetryCount: 2,
          retryIntervalSeconds: 60,
          nextRunTime: '2026-10-13T01:00:00Z',
        },
      ],
    })
    renderTimers()

    expect(await screen.findByText('每日报告')).toBeInTheDocument()
    expect(screen.getByText('销售助手')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /新建定时器/ })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /立即执行/ })).toBeInTheDocument()
    expect(screen.getByText('失败重试 2 次，间隔 60 秒')).toBeInTheDocument()
  })

  it('非管理成员只读：无新建按钮与操作列', async () => {
    renderTimers(false)
    await waitFor(() => {
      expect(getTeamTimers).toHaveBeenCalled()
    })
    await waitFor(() => {
      expect(screen.queryByRole('button', { name: /新建定时器/ })).toBeNull()
    })
    expect(screen.queryByText('操作')).toBeNull()
  })

  it('新建弹窗展示绑定应用/调度/执行内容字段，必填校验拦截空提交', async () => {
    vi.mocked(getApps).mockResolvedValue({
      teamId: '7',
      myRole: 1,
      items: [
        { appId: 'a-1', teamId: '7', name: '销售助手', appType: 'agent', publishStatus: 1 },
      ],
    })
    renderTimers()
    fireEvent.click(await screen.findByRole('button', { name: /新建定时器/ }))

    const modal = await screen.findByRole('dialog')
    expect(within(modal).getByText('绑定应用')).toBeInTheDocument()
    expect(within(modal).getByText(/调度表达式/)).toBeInTheDocument()
    expect(within(modal).getByText('执行内容')).toBeInTheDocument()

    fireEvent.click(within(modal).getByRole('button', { name: /保\s*存/ }))
    await waitFor(
      () => {
        expect(createTeamTimer).not.toHaveBeenCalled()
      },
      { timeout: 1500 },
    )
  })

  it('立即执行触发后打开历史抽屉（默认加载 10 条）', async () => {
    vi.mocked(getTeamTimers).mockResolvedValue({
      totalCount: 1,
      items: [
        {
          timerId: 't-1',
          teamId: '7',
          name: '每日报告',
          appId: 'a-1',
          appName: '销售助手',
          appType: 'agent',
          appPublishStatus: 1,
          isEnabled: true,
          cronExpression: '0 1 * * *',
          inputContent: '生成今日日报',
          maxRetryCount: 0,
          retryIntervalSeconds: 60,
        },
      ],
    })
    vi.mocked(getTeamTimerExecutions).mockResolvedValue({
      totalCount: 1,
      items: [
        {
          executionId: 'e-1',
          timerId: 't-1',
          triggerType: 'manual',
          status: 'running',
          attemptCount: 1,
          createTime: '2026-10-12T00:00:00Z',
          triggerUserName: 'admin',
        },
      ],
    })
    renderTimers()
    fireEvent.click(await screen.findByRole('button', { name: /立即执行/ }))

    await waitFor(() => {
      expect(runTeamTimer).toHaveBeenCalledWith('t-1')
    })
    expect(await screen.findByText('执行历史（每日报告）')).toBeInTheDocument()
    expect(getTeamTimerExecutions).toHaveBeenCalledWith('t-1', 1, 10)
  })
})

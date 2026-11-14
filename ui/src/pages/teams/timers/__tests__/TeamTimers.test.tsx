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
  TIMER_STATUS: {
    Pending: 'pending',
    Running: 'running',
    Success: 'success',
    Failed: 'failed',
    Retrying: 'retrying',
  },
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

/** 打开绑定应用下拉（弹窗内还有调度表达式的频率下拉，须按 #appId 定位） */
function openAppSelect(modal: HTMLElement) {
  const appSelect = modal.querySelector('#appId')?.closest('.ant-select')
  expect(appSelect).toBeTruthy()
  fireEvent.mouseDown(appSelect!.querySelector('.ant-select-selector')!)
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
    expect(screen.getByRole('button', { name: /新建自动化/ })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /立即执行/ })).toBeInTheDocument()
    expect(screen.getByText('失败重试 2 次，间隔 60 秒')).toBeInTheDocument()
  })

  it('非管理成员只读：无新建按钮与操作列', async () => {
    renderTimers(false)
    await waitFor(() => {
      expect(getTeamTimers).toHaveBeenCalled()
    })
    await waitFor(() => {
      expect(screen.queryByRole('button', { name: /新建自动化/ })).toBeNull()
    })
    expect(screen.queryByText('操作')).toBeNull()
  })

  it('新建弹窗展示绑定应用/调度/执行指令字段，必填校验拦截空提交', async () => {
    vi.mocked(getApps).mockResolvedValue({
      teamId: '7',
      myRole: 1,
      items: [{ appId: 'a-1', teamId: '7', name: '销售助手', appType: 'agent', publishStatus: 1 }],
    })
    renderTimers()
    fireEvent.click(await screen.findByRole('button', { name: /新建自动化/ }))

    const modal = await screen.findByRole('dialog')
    expect(within(modal).getByText('绑定应用')).toBeInTheDocument()
    expect(within(modal).getByText(/调度表达式/)).toBeInTheDocument()
    expect(within(modal).getByText('执行指令')).toBeInTheDocument()
    // 调度表达式选择器：常用/自定义切换 + 北京时间描述（默认值 0 1 * * * = 每天 09:00）
    expect(within(modal).getByText('常用')).toBeInTheDocument()
    expect(within(modal).getByText('自定义')).toBeInTheDocument()
    expect(within(modal).getByText('每天 09:00 执行')).toBeInTheDocument()

    fireEvent.click(within(modal).getByRole('button', { name: /保\s*存/ }))
    await waitFor(
      () => {
        expect(createTeamTimer).not.toHaveBeenCalled()
      },
      { timeout: 1500 },
    )
  })

  it('绑定应用下拉只展示已发布应用，未发布的直接过滤', async () => {
    vi.mocked(getApps).mockResolvedValue({
      teamId: '7',
      myRole: 1,
      items: [
        { appId: 'a-1', teamId: '7', name: '已发布助手', appType: 'agent', publishStatus: 1 },
        { appId: 'a-2', teamId: '7', name: '草稿应用', appType: 'workflow', publishStatus: 0 },
      ],
    })
    renderTimers()
    fireEvent.click(await screen.findByRole('button', { name: /新建自动化/ }))

    const modal = await screen.findByRole('dialog')
    openAppSelect(modal)
    await waitFor(() => {
      expect(screen.getByText('已发布助手')).toBeInTheDocument()
    })
    expect(screen.queryByText('草稿应用')).toBeNull()
  })

  it('没有已发布应用时下拉显示空态提示', async () => {
    vi.mocked(getApps).mockResolvedValue({
      teamId: '7',
      myRole: 1,
      items: [{ appId: 'a-2', teamId: '7', name: '草稿应用', appType: 'workflow', publishStatus: 0 }],
    })
    renderTimers()
    fireEvent.click(await screen.findByRole('button', { name: /新建自动化/ }))

    const modal = await screen.findByRole('dialog')
    openAppSelect(modal)
    await waitFor(() => {
      expect(screen.getByText('暂无已发布应用，请先发布应用')).toBeInTheDocument()
    })
  })

  it('不开启重试时无需填写重试间隔，提交兜底默认 60 秒', async () => {
    vi.mocked(getApps).mockResolvedValue({
      teamId: '7',
      myRole: 1,
      items: [{ appId: 'a-1', teamId: '7', name: '已发布助手', appType: 'agent', publishStatus: 1 }],
    })
    renderTimers()
    fireEvent.click(await screen.findByRole('button', { name: /新建自动化/ }))

    const modal = await screen.findByRole('dialog')
    fireEvent.change(within(modal).getByPlaceholderText('如：每日销售报告'), { target: { value: '每日报告' } })
    fireEvent.change(within(modal).getByPlaceholderText(/汇总本周数据/), { target: { value: '生成日报' } })
    openAppSelect(modal)
    const option = await waitFor(() => {
      // 选项 label = 名称 + 类型 Tag，用包含匹配定位
      const el = [...document.querySelectorAll('.ant-select-item-option')].find((o) =>
        o.textContent?.includes('已发布助手'),
      )
      expect(el).toBeTruthy()
      return el!
    })
    fireEvent.click(option)

    // 失败重试保持默认 0：重试间隔输入框不渲染，保存不应报「重试间隔仅允许 10-3600 秒」
    expect(within(modal).queryByLabelText(/重试间隔/)).toBeNull()
    fireEvent.click(within(modal).getByRole('button', { name: /保\s*存/ }))

    await waitFor(() => {
      expect(createTeamTimer).toHaveBeenCalledWith(
        7,
        expect.objectContaining({ maxRetryCount: 0, retryIntervalSeconds: 60 }),
      )
    })
  })

  it('执行指令列超长 URL 不撑爆表格：省略号截断并悬停全文', async () => {
    const longUrl = 'https://example.com/api/callback?token=' + 'x'.repeat(300)
    vi.mocked(getTeamTimers).mockResolvedValue({
      totalCount: 1,
      items: [
        {
          timerId: 't-1',
          teamId: '7',
          name: '回调任务',
          appId: 'a-1',
          appName: '销售助手',
          appType: 'agent',
          appPublishStatus: 1,
          isEnabled: true,
          cronExpression: '0 1 * * *',
          inputContent: longUrl,
          maxRetryCount: 0,
          retryIntervalSeconds: 60,
        },
      ],
    })
    renderTimers()

    const cell = await screen.findByTitle(longUrl)
    expect(cell.className).toContain('ant-table-cell-ellipsis')
  })

  it('执行状态列：活跃呼吸灯、终态结果、未执行不显示状态', async () => {
    vi.mocked(getTeamTimers).mockResolvedValue({
      totalCount: 3,
      items: [
        {
          timerId: 't-1',
          teamId: '7',
          name: '进行中任务',
          appId: 'a-1',
          appName: '销售助手',
          appType: 'agent',
          appPublishStatus: 1,
          isEnabled: true,
          cronExpression: '0 1 * * *',
          inputContent: '生成日报',
          maxRetryCount: 0,
          retryIntervalSeconds: 60,
          lastExecutionStatus: 'running',
        },
        {
          timerId: 't-2',
          teamId: '7',
          name: '失败任务',
          appId: 'a-1',
          appName: '销售助手',
          appType: 'agent',
          appPublishStatus: 1,
          isEnabled: true,
          cronExpression: '0 1 * * *',
          inputContent: '生成周报',
          maxRetryCount: 0,
          retryIntervalSeconds: 60,
          lastExecutionStatus: 'failed',
        },
        {
          timerId: 't-3',
          teamId: '7',
          name: '未执行任务',
          appId: 'a-1',
          appName: '销售助手',
          appType: 'agent',
          appPublishStatus: 1,
          isEnabled: true,
          cronExpression: '0 1 * * *',
          inputContent: '生成月报',
          maxRetryCount: 0,
          retryIntervalSeconds: 60,
        },
      ],
    })
    renderTimers()

    expect(await screen.findByText('执行中')).toBeInTheDocument()
    expect(screen.getByText('失败')).toBeInTheDocument()
    // 未执行过的行不出现状态文案（仅活跃/终态 Badge 有文案）
    expect(screen.queryByText('成功')).toBeNull()
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

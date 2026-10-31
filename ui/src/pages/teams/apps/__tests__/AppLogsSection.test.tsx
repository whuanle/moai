import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import '@/i18n'
import { AppLogsSection } from '../AppLogsSection'
import { getAppLogs, getAppLogMessages, getAppSecurityEvents, getAppUsage } from '@/api/app'

vi.mock('@/api/app', () => ({
  getAppLogs: vi.fn(),
  getAppLogMessages: vi.fn(),
  getAppSecurityEvents: vi.fn(),
  getAppUsage: vi.fn(),
}))

vi.mock('@/api/memory', () => ({ getAppMemoryStats: vi.fn().mockResolvedValue({ totalMemories: 0 }) }))

const NORMAL_LOG = {
  sessionId: 's1',
  title: '售前咨询会话',
  userType: 'normal',
  ownerId: '7',
  totalTokens: 1234,
  lastMessageTime: '2026-09-01T10:00:00Z',
  createTime: '2026-09-01T09:00:00Z',
  createUserName: '张三',
}

const EXTERNAL_LOG = {
  sessionId: 's2',
  title: '外部访客会话',
  userType: 'external',
  ownerId: '999',
  totalTokens: 10,
  createTime: '2026-09-02T09:00:00Z',
}

// 存量脏数据：刷新链路曾漏设 UserType，内部会话落 user_type=0（none）
const NONE_LOG = {
  sessionId: 's3',
  title: '内部用户历史会话',
  userType: 'none',
  ownerId: '1',
  totalTokens: 8,
  createTime: '2026-09-03T09:00:00Z',
}

describe('AppLogsSection（应用日志分区：日志/安全事件/使用分析 Tab）', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(getAppLogs).mockResolvedValue({
      items: [NORMAL_LOG, EXTERNAL_LOG, NONE_LOG],
      total: 3,
      pageNo: 1,
      pageSize: 20,
    })
    vi.mocked(getAppLogMessages).mockResolvedValue([
      { messageId: 'm1', seq: 1, role: 'user', content: '你好', toolCalls: null },
      { messageId: 'm2', seq: 2, role: 'assistant', content: '您好，请问有什么可以帮您？', toolCalls: null },
    ])
    vi.mocked(getAppSecurityEvents).mockResolvedValue({ items: [], total: 0, pageNo: 1, pageSize: 10 })
    vi.mocked(getAppUsage).mockResolvedValue({
      summary: { callCount: 12, promptTokens: 100, completionTokens: 50, totalTokens: 150, cachedTokens: 60 },
      byModel: [],
      stats: { useCount: 5, sessionCount: 3, userCount: 2, messageCount: 8, lastActiveTime: null },
    })
  })

  it('默认展示日志 Tab：会话标题、用户与合计 Token', async () => {
    render(<AppLogsSection appId="a1" canManage />)

    expect(await screen.findByText('售前咨询会话')).toBeTruthy()
    expect(screen.getByText('张三')).toBeTruthy()
    expect(screen.getByText('1234')).toBeTruthy()
    expect(screen.getByRole('tab', { name: '日志' }).getAttribute('aria-selected')).toBe('true')
    await waitFor(() =>
      expect(getAppLogs).toHaveBeenCalledWith('a1', expect.objectContaining({ pageNo: 1, pageSize: 20 })),
    )
    // 非激活 Tab 内容不挂载：安全事件与使用分析未请求
    expect(getAppSecurityEvents).not.toHaveBeenCalled()
    expect(getAppUsage).not.toHaveBeenCalled()
  })

  it('外部用户显示「外部用户 #id」', async () => {
    render(<AppLogsSection appId="a1" canManage />)

    expect(await screen.findByText('外部用户 #999')).toBeTruthy()
  })

  it('用户类型识别不到（none）只显示 #id，不误标为外部用户', async () => {
    render(<AppLogsSection appId="a1" canManage />)

    expect(await screen.findByText('#1')).toBeTruthy()
    // 用户类型标签仍如实展示「未知」
    expect(screen.getByText('未知')).toBeTruthy()
  })

  it('点击「查看」拉取消息并展示抽屉内容', async () => {
    render(<AppLogsSection appId="a1" canManage />)

    const viewButtons = await screen.findAllByRole('button', { name: /查\s*看/ })
    fireEvent.click(viewButtons[0])

    await waitFor(() => expect(getAppLogMessages).toHaveBeenCalledWith('a1', 's1'))
    expect(await screen.findByText('你好')).toBeTruthy()
    expect(screen.getByText('您好，请问有什么可以帮您？')).toBeTruthy()
  })

  it('消息详情抽屉按会话式排版渲染：用户消息靠右、助手消息靠左，非容器盒子堆叠', async () => {
    render(<AppLogsSection appId="a1" canManage />)

    const viewButtons = await screen.findAllByRole('button', { name: /查\s*看/ })
    fireEvent.click(viewButtons[0])

    const stream = await screen.findByText('你好').then(() => document.querySelector('.moai-chat__stream'))
    expect(stream).toBeTruthy()
    expect(document.querySelector('.moai-chat__row--user .moai-chat__bubble')).toBeTruthy()
    expect(document.querySelector('.moai-chat__row--assistant .moai-chat__markdown')).toBeTruthy()
    // 旧版逐条边框容器不再使用
    expect(stream!.querySelector('.moai-chat__row--user')!.contains(screen.getByText('你好'))).toBe(true)
  })

  it('「安全事件」Tab 展示规则命中审计（懒加载：切换才请求）', async () => {
    vi.mocked(getAppSecurityEvents).mockResolvedValue({
      items: [
        {
          id: 'e1',
          sessionId: 's1',
          userId: '1',
          scope: 'toolResult',
          ruleName: '',
          ruleType: 'bankCard',
          strategy: 'block',
          action: 'blocked',
          hitCount: 3,
          createTime: '2026-10-13T08:30:00+08:00',
        },
      ],
      total: 1,
      pageNo: 1,
      pageSize: 10,
    })

    render(<AppLogsSection appId="a1" canManage />)

    expect(getAppSecurityEvents).not.toHaveBeenCalled()
    fireEvent.click(screen.getByRole('tab', { name: '安全事件' }))

    expect(await screen.findByText('工具结果')).toBeTruthy()
    expect(screen.getByText('已拦截')).toBeTruthy()
    expect(screen.getByText('银行卡号')).toBeTruthy()
    expect(screen.getByText('3')).toBeTruthy()
    expect(getAppSecurityEvents).toHaveBeenCalledWith('a1', 1, 10)
  })

  it('「使用分析」Tab 展示用量汇总（懒加载：切换才请求，原「监控」分区内容）', async () => {
    render(<AppLogsSection appId="a1" canManage />)

    expect(getAppUsage).not.toHaveBeenCalled()
    fireEvent.click(screen.getByRole('tab', { name: '使用分析' }))

    await waitFor(() => expect(getAppUsage).toHaveBeenCalledWith('a1'))
    expect(await screen.findByText('12')).toBeTruthy()
  })

  it('非管理员仅保留日志 Tab，无安全事件与使用分析', async () => {
    render(<AppLogsSection appId="a1" canManage={false} />)

    expect(await screen.findByText('售前咨询会话')).toBeTruthy()
    expect(screen.getByRole('tab', { name: '日志' })).toBeTruthy()
    expect(screen.queryByRole('tab', { name: '安全事件' })).toBeNull()
    expect(screen.queryByRole('tab', { name: '使用分析' })).toBeNull()
  })

  it('defaultTab="usage"（旧 /monitor 深链）直接落到使用分析', async () => {
    render(<AppLogsSection appId="a1" canManage defaultTab="usage" />)

    expect(screen.getByRole('tab', { name: '使用分析' }).getAttribute('aria-selected')).toBe('true')
    await waitFor(() => expect(getAppUsage).toHaveBeenCalledWith('a1'))
  })
})

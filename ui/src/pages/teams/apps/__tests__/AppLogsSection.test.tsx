import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import '@/i18n'
import { AppLogsSection } from '../AppLogsSection'
import { getAppLogs, getAppLogMessages } from '@/api/app'

vi.mock('@/api/app', () => ({
  getAppLogs: vi.fn(),
  getAppLogMessages: vi.fn(),
}))

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

describe('AppLogsSection（应用对话日志）', () => {
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
  })

  it('渲染会话标题、用户与合计 Token', async () => {
    render(<AppLogsSection appId="a1" />)

    expect(await screen.findByText('售前咨询会话')).toBeTruthy()
    expect(screen.getByText('张三')).toBeTruthy()
    expect(screen.getByText('1234')).toBeTruthy()
    await waitFor(() =>
      expect(getAppLogs).toHaveBeenCalledWith('a1', expect.objectContaining({ pageNo: 1, pageSize: 20 })),
    )
  })

  it('外部用户显示「外部用户 #id」', async () => {
    render(<AppLogsSection appId="a1" />)

    expect(await screen.findByText('外部用户 #999')).toBeTruthy()
  })

  it('用户类型识别不到（none）只显示 #id，不误标为外部用户', async () => {
    render(<AppLogsSection appId="a1" />)

    expect(await screen.findByText('#1')).toBeTruthy()
    // 用户类型标签仍如实展示「未知」
    expect(screen.getByText('未知')).toBeTruthy()
  })

  it('点击「查看」拉取消息并展示抽屉内容', async () => {
    render(<AppLogsSection appId="a1" />)

    const viewButtons = await screen.findAllByRole('button', { name: '查看' })
    fireEvent.click(viewButtons[0])

    await waitFor(() => expect(getAppLogMessages).toHaveBeenCalledWith('a1', 's1'))
    expect(await screen.findByText('你好')).toBeTruthy()
    expect(screen.getByText('您好，请问有什么可以帮您？')).toBeTruthy()
  })
})

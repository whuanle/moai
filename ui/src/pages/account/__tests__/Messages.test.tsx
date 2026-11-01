import '@/i18n'
import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Messages } from '../Messages'
import { useNotificationStore } from '@/store/notification'
import { deleteMessages, getMessages, getUnreadCount, markAllMessagesRead, markMessagesRead, type MessageItem } from '@/api/message'

vi.mock('@/api/message', () => ({
  getMessages: vi.fn(),
  getUnreadCount: vi.fn(),
  markMessagesRead: vi.fn(),
  markAllMessagesRead: vi.fn(),
  deleteMessages: vi.fn(),
}))

const items: MessageItem[] = [
  {
    id: '11',
    title: '系统维护通知',
    content: '平台将于周日凌晨进行维护。',
    isRead: false,
    messageType: 'system',
    source: 'platform',
    senderUserId: null,
    params: null,
    readTime: null,
    createUserName: '',
    createTime: '2026-11-01T10:00:00+08:00',
    updateUserName: '',
    updateTime: '2026-11-01T10:00:00+08:00',
  },
  {
    id: '12',
    title: '团队邀请',
    content: '你已被加入团队 A。',
    isRead: true,
    messageType: 'team',
    source: 'team',
    senderUserId: '2',
    params: null,
    readTime: '2026-11-01T11:00:00+08:00',
    createUserName: '张三',
    createTime: '2026-11-01T09:00:00+08:00',
    updateUserName: '',
    updateTime: '2026-11-01T09:00:00+08:00',
  },
]

function mockList(itemsOverride?: typeof items) {
  vi.mocked(getMessages).mockResolvedValue({
    items: itemsOverride ?? items,
    total: itemsOverride?.length ?? items.length,
    pageNo: 1,
    pageSize: 10,
  })
}

function renderPage() {
  return render(<Messages />)
}

describe('Messages', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockList()
    vi.mocked(getUnreadCount).mockResolvedValue(2)
    vi.mocked(markMessagesRead).mockResolvedValue(undefined)
    vi.mocked(markAllMessagesRead).mockResolvedValue(undefined)
    vi.mocked(deleteMessages).mockResolvedValue(undefined)
    useNotificationStore.setState({ unreadCount: 0 })
  })

  it('渲染消息列表：未读行带标记已读操作，系统消息发送人回退为「系统」', async () => {
    renderPage()
    expect(await screen.findByText('系统维护通知')).toBeInTheDocument()
    expect(screen.getByText('团队邀请')).toBeInTheDocument()
    expect(screen.getByText('系统')).toBeInTheDocument()
    expect(screen.getByText('张三')).toBeInTheDocument()
    // 仅未读行渲染「标记已读」操作
    expect(screen.getAllByText('标记已读')).toHaveLength(1)
  })

  it('点击未读行调用标记已读并刷新列表与角标', async () => {
    const user = userEvent.setup()
    renderPage()
    const row = await screen.findByText('系统维护通知')
    await user.click(row)
    await waitFor(() => expect(markMessagesRead).toHaveBeenCalledWith(['11']))
    await waitFor(() => expect(getUnreadCount).toHaveBeenCalled())
  })

  it('「全部已读」确认后调用接口', async () => {
    const user = userEvent.setup()
    renderPage()
    await screen.findByText('系统维护通知')
    await user.click(screen.getByRole('button', { name: /全部已读/ }))
    // Popconfirm 确认按钮：antd 默认 en 为 OK，zh 为 确定
    await user.click(await screen.findByRole('button', { name: /^(OK|确\s*定)$/ }))
    await waitFor(() => expect(markAllMessagesRead).toHaveBeenCalled())
  })

  it('删除需 Popconfirm 确认，确认后调用删除接口', async () => {
    const user = userEvent.setup()
    renderPage()
    await screen.findByText('系统维护通知')
    // 行容器也带 role=button，取「删除」文本所在的真实按钮
    const deleteButton = screen.getAllByText('删除')[0]!.closest('button')!
    await user.click(deleteButton)
    await user.click(await screen.findByRole('button', { name: /^(OK|确\s*定)$/ }))
    await waitFor(() => expect(deleteMessages).toHaveBeenCalled())
  })

  it('切换「未读」过滤以 isRead=false 重新查询', async () => {
    const user = userEvent.setup()
    renderPage()
    await screen.findByText('系统维护通知')
    await user.click(screen.getByText('未读'))
    await waitFor(() => expect(getMessages).toHaveBeenCalledWith(expect.objectContaining({ isRead: false, pageNo: 1 })))
  })

  it('空列表展示空态', async () => {
    mockList([])
    renderPage()
    expect(await screen.findByText('暂无消息')).toBeInTheDocument()
  })
})

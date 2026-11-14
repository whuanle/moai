import '@/i18n'
import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Messages } from '../Messages'
import { useNotificationStore } from '@/store/notification'
import { deleteMessages, getMessages, getUnreadCount, markMessagesRead, type MessageItem } from '@/api/message'

vi.mock('@/api/message', () => ({
  getMessages: vi.fn(),
  getUnreadCount: vi.fn(),
  markMessagesRead: vi.fn(),
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
    pageSize: 20,
  })
}

function renderPage() {
  return render(<Messages />)
}

/** 工具栏/行内复选框：[0]=全选，其余按行序 */
function rowCheckboxes() {
  return screen.getAllByRole('checkbox')
}

describe('Messages', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockList()
    vi.mocked(getUnreadCount).mockResolvedValue(2)
    vi.mocked(markMessagesRead).mockResolvedValue(undefined)
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

  it('默认按每页 20 条查询', async () => {
    renderPage()
    await screen.findByText('系统维护通知')
    expect(getMessages).toHaveBeenCalledWith(expect.objectContaining({ pageNo: 1, pageSize: 20 }))
  })

  it('点击未读行调用标记已读并刷新列表与角标', async () => {
    const user = userEvent.setup()
    renderPage()
    const row = await screen.findByText('系统维护通知')
    await user.click(row)
    await waitFor(() => expect(markMessagesRead).toHaveBeenCalledWith(['11']))
    await waitFor(() => expect(getUnreadCount).toHaveBeenCalled())
  })

  it('头部渲染批量操作按钮，不再提供「全部已读」', async () => {
    renderPage()
    await screen.findByText('系统维护通知')
    // 批量已读/批量删除在页头（右上角），随勾选启用
    expect(screen.getByRole('button', { name: /批量已读/ })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /批量删除/ })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /全部已读/ })).not.toBeInTheDocument()
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

  it('未选中时批量已读/批量删除禁用', async () => {
    renderPage()
    await screen.findByText('系统维护通知')
    expect(screen.getByRole('button', { name: /批量已读/ })).toBeDisabled()
    expect(screen.getByRole('button', { name: /批量删除/ })).toBeDisabled()
  })

  it('勾选未读行后批量已读可用，批量删除因无可删已读项而禁用', async () => {
    const user = userEvent.setup()
    renderPage()
    await screen.findByText('系统维护通知')
    await user.click(rowCheckboxes()[1]!) // 未读行
    expect(screen.getByText('已选 1 条')).toBeInTheDocument()
    const batchReadButton = screen.getByRole('button', { name: /批量已读/ })
    expect(batchReadButton).toBeEnabled()
    // 批量删除只删已读：仅勾选未读时无可删项，禁用
    expect(screen.getByRole('button', { name: /批量删除/ })).toBeDisabled()
    await user.click(batchReadButton)
    await waitFor(() => expect(markMessagesRead).toHaveBeenCalledWith(['11']))
  })

  it('全选后批量删除只删已读项，未读自动忽略并在气泡提示', async () => {
    const user = userEvent.setup()
    renderPage()
    await screen.findByText('系统维护通知')
    await user.click(rowCheckboxes()[0]!) // 全选（1 未读 + 1 已读）
    expect(screen.getByText('已选 2 条')).toBeInTheDocument()
    // 选中含未读消息，批量已读可用
    expect(screen.getByRole('button', { name: /批量已读/ })).toBeEnabled()
    await user.click(screen.getByRole('button', { name: /批量删除/ }))
    // 气泡按实际可删的已读条数展示，并提示未读忽略
    expect(await screen.findByText('确定删除选中的 1 条已读消息吗？未读 1 条将自动忽略')).toBeInTheDocument()
    await user.click(await screen.findByRole('button', { name: /^(OK|确\s*定)$/ }))
    await waitFor(() => expect(deleteMessages).toHaveBeenCalledWith(['12']))
  })

  it('切换过滤后清空选择并禁用批量按钮', async () => {
    const user = userEvent.setup()
    renderPage()
    await screen.findByText('系统维护通知')
    await user.click(rowCheckboxes()[1]!)
    expect(screen.getByText('已选 1 条')).toBeInTheDocument()
    await user.click(screen.getByText('未读'))
    await waitFor(() => expect(screen.getByText('已选 0 条')).toBeInTheDocument())
    expect(screen.getByRole('button', { name: /批量删除/ })).toBeDisabled()
  })
})

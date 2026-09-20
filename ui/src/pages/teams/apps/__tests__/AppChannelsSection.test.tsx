import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { AppChannelsSection } from '../AppChannelsSection'
import {
  bindFeishuApp,
  createFeishuApp,
  deleteFeishuApp,
  getFeishuApps,
  unbindFeishuApp,
  updateFeishuApp,
} from '@/api/feishuApp'

vi.mock('@/api/feishuApp', () => ({
  getFeishuApps: vi.fn(),
  createFeishuApp: vi.fn(),
  updateFeishuApp: vi.fn(),
  deleteFeishuApp: vi.fn(),
  bindFeishuApp: vi.fn(),
  unbindFeishuApp: vi.fn(),
}))

const ITEMS = [
  {
    feishuAppId: 'f1',
    teamId: 3,
    name: '客服机器人',
    appId: 'cli_bound',
    isDisable: false,
    isOnline: true,
    bindChannelType: 'app',
    bindChannelId: 'a1',
    bindTime: '2026-09-01T08:00:00Z',
    createUserName: 'admin',
  },
  {
    feishuAppId: 'f2',
    teamId: 3,
    name: '闲置连接',
    appId: 'cli_free',
    isDisable: false,
    isOnline: false,
    bindChannelType: null,
    bindChannelId: null,
  },
  {
    feishuAppId: 'f3',
    teamId: 3,
    name: '其它应用渠道',
    appId: 'cli_other',
    isDisable: false,
    isOnline: false,
    bindChannelType: 'app',
    bindChannelId: 'other-app',
  },
]

function renderSection(props?: Partial<Parameters<typeof AppChannelsSection>[0]>) {
  return render(
    <AppChannelsSection teamId={3} appId="a1" canManage isPublished {...props} />,
  )
}

beforeEach(() => {
  vi.clearAllMocks()
  vi.mocked(getFeishuApps).mockResolvedValue({ teamId: 3, myRole: 2, items: ITEMS })
  vi.mocked(createFeishuApp).mockResolvedValue('f-new')
  vi.mocked(bindFeishuApp).mockResolvedValue(undefined)
  vi.mocked(unbindFeishuApp).mockResolvedValue(undefined)
  vi.mocked(updateFeishuApp).mockResolvedValue(undefined)
  vi.mocked(deleteFeishuApp).mockResolvedValue(undefined)
})

describe('AppChannelsSection（应用外部渠道）', () => {
  it('只展示绑定到当前应用的飞书连接，含状态与接入指引', async () => {
    renderSection()
    expect(await screen.findByText('客服机器人')).toBeInTheDocument()
    expect(screen.getByText('cli_bound')).toBeInTheDocument()
    expect(screen.getByText('在线')).toBeInTheDocument()
    expect(screen.queryByText('闲置连接')).not.toBeInTheDocument()
    expect(screen.queryByText('其它应用渠道')).not.toBeInTheDocument()
    expect(screen.getByText('接入指引')).toBeInTheDocument()
    expect(screen.queryByText(/应用尚未发布/)).not.toBeInTheDocument()
  })

  it('应用未发布时提示消息不会被处理', async () => {
    renderSection({ isPublished: false })
    expect(await screen.findByText(/应用尚未发布/)).toBeInTheDocument()
  })

  it('接入飞书应用：创建连接并绑定到当前应用', async () => {
    renderSection()
    fireEvent.click(await screen.findByRole('button', { name: /接\s*入\s*飞\s*书\s*应\s*用/ }))

    fireEvent.change(screen.getByLabelText('连接名称'), { target: { value: '新机器人' } })
    fireEvent.change(screen.getByLabelText('AppID'), { target: { value: 'cli_new' } })
    fireEvent.change(screen.getByLabelText('AppSecret'), { target: { value: 'secret-1' } })
    fireEvent.click(screen.getByRole('button', { name: /创\s*建\s*并\s*绑\s*定/ }))

    await waitFor(() =>
      expect(createFeishuApp).toHaveBeenCalledWith({
        teamId: 3,
        name: '新机器人',
        description: undefined,
        appId: 'cli_new',
        appSecret: 'secret-1',
        domain: undefined,
      }),
    )
    await waitFor(() => expect(bindFeishuApp).toHaveBeenCalledWith('f-new', 'app', 'a1'))
    // 弹窗关闭与否依赖 antd 动画在 jsdom 的结束事件，按项目惯例不对其断言
  })

  it('绑定已有连接：仅可选未绑定连接', async () => {
    renderSection()
    fireEvent.click(await screen.findByRole('button', { name: /绑\s*定\s*已\s*有\s*连\s*接/ }))

    fireEvent.mouseDown(screen.getByRole('combobox'))
    const option = await screen.findByText('闲置连接（cli_free）')
    fireEvent.click(option)

    fireEvent.click(screen.getByRole('button', { name: /绑\s*定$/ }))
    await waitFor(() => expect(bindFeishuApp).toHaveBeenCalledWith('f2', 'app', 'a1'))
  })

  it('无未绑定连接时「绑定已有连接」不可用', async () => {
    vi.mocked(getFeishuApps).mockResolvedValue({ teamId: 3, myRole: 2, items: [ITEMS[0]] })
    renderSection()
    const btn = await screen.findByRole('button', { name: /绑\s*定\s*已\s*有\s*连\s*接/ })
    expect(btn).toBeDisabled()
  })

  it('解绑需二次确认', async () => {
    renderSection()
    fireEvent.click(await screen.findByRole('button', { name: /解\s*绑/ }))
    fireEvent.click(await screen.findByRole('button', { name: /确\s*定/ }))
    await waitFor(() => expect(unbindFeishuApp).toHaveBeenCalledWith('f1'))
  })

  it('停用需二次确认，启用直接执行', async () => {
    renderSection()
    fireEvent.click(await screen.findByRole('button', { name: /停\s*用/ }))
    fireEvent.click(await screen.findByRole('button', { name: /确\s*定/ }))
    await waitFor(() =>
      expect(updateFeishuApp).toHaveBeenCalledWith('f1', expect.objectContaining({ isDisable: true })),
    )
  })

  it('删除需二次确认', async () => {
    renderSection()
    fireEvent.click(await screen.findByRole('button', { name: /删\s*除/ }))
    fireEvent.click(await screen.findByRole('button', { name: /确\s*定/ }))
    await waitFor(() => expect(deleteFeishuApp).toHaveBeenCalledWith('f1'))
  })

  it('非管理员只读：无操作列与工具栏按钮', async () => {
    renderSection({ canManage: false })
    expect(await screen.findByText('客服机器人')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /接\s*入\s*飞\s*书\s*应\s*用/ })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /解\s*绑/ })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /删\s*除/ })).not.toBeInTheDocument()
  })
})

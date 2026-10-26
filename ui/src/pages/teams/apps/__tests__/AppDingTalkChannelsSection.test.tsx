import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { AppDingTalkChannelsSection } from '../AppDingTalkChannelsSection'
import {
  createDingTalkApp,
  deleteDingTalkApp,
  getDingTalkApps,
  updateDingTalkApp,
} from '@/api/dingtalkApp'

vi.mock('@/api/dingtalkApp', () => ({
  getDingTalkApps: vi.fn(),
  createDingTalkApp: vi.fn(),
  updateDingTalkApp: vi.fn(),
  deleteDingTalkApp: vi.fn(),
}))

const ITEMS = [
  {
    dingTalkAppId: 'd1',
    teamId: 3,
    name: '客服机器人',
    appKey: 'ding_bound',
    isDisable: false,
    isOnline: true,
    bindChannelType: 'app',
    bindChannelId: 'a1',
    bindTime: '2026-10-01T08:00:00Z',
    createUserName: 'admin',
  },
  {
    dingTalkAppId: 'd2',
    teamId: 3,
    name: '闲置连接',
    appKey: 'ding_free',
    isDisable: false,
    isOnline: false,
    bindChannelType: null,
    bindChannelId: null,
  },
  {
    dingTalkAppId: 'd3',
    teamId: 3,
    name: '其它应用渠道',
    appKey: 'ding_other',
    isDisable: false,
    isOnline: false,
    bindChannelType: 'app',
    bindChannelId: 'other-app',
  },
]

function renderSection(props?: Partial<Parameters<typeof AppDingTalkChannelsSection>[0]>) {
  return render(
    <AppDingTalkChannelsSection teamId={3} appId="a1" canManage isPublished {...props} />,
  )
}

beforeEach(() => {
  vi.clearAllMocks()
  vi.mocked(getDingTalkApps).mockResolvedValue({ teamId: 3, myRole: 2, items: ITEMS })
  vi.mocked(createDingTalkApp).mockResolvedValue('d-new')
  vi.mocked(updateDingTalkApp).mockResolvedValue(undefined)
  vi.mocked(deleteDingTalkApp).mockResolvedValue(undefined)
})

describe('AppDingTalkChannelsSection（应用外部渠道·钉钉）', () => {
  it('只展示接入到当前应用的钉钉连接，含状态与接入指引', async () => {
    renderSection()
    expect(await screen.findByText('客服机器人')).toBeInTheDocument()
    expect(screen.getByText('ding_bound')).toBeInTheDocument()
    expect(screen.getByText('在线')).toBeInTheDocument()
    expect(screen.queryByText('闲置连接')).not.toBeInTheDocument()
    expect(screen.queryByText('其它应用渠道')).not.toBeInTheDocument()
    expect(screen.getByText('接入指引')).toBeInTheDocument()
    expect(screen.getByText('钉钉渠道')).toBeInTheDocument()
    expect(screen.queryByText(/应用尚未发布/)).not.toBeInTheDocument()
  })

  it('应用未发布时提示消息不会被处理', async () => {
    renderSection({ isPublished: false })
    expect(await screen.findByText(/应用尚未发布/)).toBeInTheDocument()
  })

  it('接入钉钉应用：创建连接即绑定到当前应用（单次调用）', async () => {
    renderSection()
    fireEvent.click(await screen.findByRole('button', { name: /接\s*入\s*钉\s*钉\s*应\s*用/ }))

    fireEvent.change(screen.getByLabelText('连接名称'), { target: { value: '新机器人' } })
    fireEvent.change(screen.getByLabelText('AppKey'), { target: { value: 'ding_new' } })
    fireEvent.change(screen.getByLabelText('AppSecret'), { target: { value: 'secret-1' } })
    fireEvent.click(screen.getByRole('button', { name: /^创\s*建$/ }))

    await waitFor(() =>
      expect(createDingTalkApp).toHaveBeenCalledWith({
        teamId: 3,
        name: '新机器人',
        description: undefined,
        appKey: 'ding_new',
        appSecret: 'secret-1',
        channelType: 'app',
        channelId: 'a1',
      }),
    )
  })

  it('编辑：回显表单，密钥留空表示保持不变（不改启停状态）', async () => {
    renderSection()
    fireEvent.click(await screen.findByRole('button', { name: /编\s*辑/ }))

    expect(await screen.findByText('编辑钉钉应用')).toBeInTheDocument()
    expect(screen.getByLabelText('连接名称')).toHaveValue('客服机器人')
    expect(screen.getByLabelText('AppKey')).toHaveValue('ding_bound')

    fireEvent.change(screen.getByLabelText('连接名称'), { target: { value: '改名机器人' } })
    fireEvent.change(screen.getByLabelText('AppKey'), { target: { value: 'ding_fixed' } })
    // 密钥留空直接保存
    fireEvent.click(screen.getByRole('button', { name: /^保\s*存$/ }))

    await waitFor(() =>
      expect(updateDingTalkApp).toHaveBeenCalledWith('d1', {
        name: '改名机器人',
        description: undefined,
        appKey: 'ding_fixed',
        appSecret: undefined,
        isDisable: false,
      }),
    )
  })

  it('编辑：填写新密钥一并保存', async () => {
    renderSection()
    fireEvent.click(await screen.findByRole('button', { name: /编\s*辑/ }))
    await screen.findByText('编辑钉钉应用')

    fireEvent.change(screen.getByLabelText('AppSecret'), { target: { value: 'new-secret' } })
    fireEvent.click(screen.getByRole('button', { name: /^保\s*存$/ }))

    await waitFor(() =>
      expect(updateDingTalkApp).toHaveBeenCalledWith('d1', expect.objectContaining({ appSecret: 'new-secret' })),
    )
  })

  it('停用需二次确认，启用直接执行', async () => {
    renderSection()
    fireEvent.click(await screen.findByRole('button', { name: /停\s*用/ }))
    fireEvent.click(await screen.findByRole('button', { name: /确\s*定/ }))
    await waitFor(() =>
      expect(updateDingTalkApp).toHaveBeenCalledWith('d1', expect.objectContaining({ isDisable: true })),
    )
  })

  it('删除需二次确认', async () => {
    renderSection()
    fireEvent.click(await screen.findByRole('button', { name: /删\s*除/ }))
    fireEvent.click(await screen.findByRole('button', { name: /确\s*定/ }))
    await waitFor(() => expect(deleteDingTalkApp).toHaveBeenCalledWith('d1'))
  })

  it('非管理员只读：无操作列与工具栏按钮', async () => {
    renderSection({ canManage: false })
    expect(await screen.findByText('客服机器人')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /接\s*入\s*钉\s*钉\s*应\s*用/ })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /停\s*用/ })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /删\s*除/ })).not.toBeInTheDocument()
  })
})

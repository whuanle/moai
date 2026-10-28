import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { AppWeixinWorkChannelsSection } from '../AppWeixinWorkChannelsSection'
import {
  createWeixinWorkApp,
  deleteWeixinWorkApp,
  getWeixinWorkApps,
  updateWeixinWorkApp,
} from '@/api/weixinworkApp'

vi.mock('@/api/weixinworkApp', () => ({
  getWeixinWorkApps: vi.fn(),
  createWeixinWorkApp: vi.fn(),
  updateWeixinWorkApp: vi.fn(),
  deleteWeixinWorkApp: vi.fn(),
}))

const ITEMS = [
  {
    weixinWorkAppId: 'w1',
    teamId: 3,
    name: '企微助手',
    corpId: 'wwcorp1',
    agentId: 1000002,
    isDisable: false,
    isOnline: true,
    bindChannelType: 'app',
    bindChannelId: 'a1',
    bindTime: '2026-10-01T08:00:00Z',
    createUserName: 'admin',
  },
  {
    weixinWorkAppId: 'w2',
    teamId: 3,
    name: '闲置连接',
    corpId: 'wwcorp2',
    agentId: 1000003,
    isDisable: false,
    isOnline: false,
    bindChannelType: null,
    bindChannelId: null,
  },
  {
    weixinWorkAppId: 'w3',
    teamId: 3,
    name: '其它应用渠道',
    corpId: 'wwcorp3',
    agentId: 1000004,
    isDisable: false,
    isOnline: false,
    bindChannelType: 'app',
    bindChannelId: 'other-app',
  },
]

function renderSection(props?: Partial<Parameters<typeof AppWeixinWorkChannelsSection>[0]>) {
  return render(
    <AppWeixinWorkChannelsSection teamId={3} appId="a1" canManage isPublished {...props} />,
  )
}

beforeEach(() => {
  vi.clearAllMocks()
  vi.mocked(getWeixinWorkApps).mockResolvedValue({ teamId: 3, myRole: 2, items: ITEMS })
  vi.mocked(createWeixinWorkApp).mockResolvedValue('w-new')
  vi.mocked(updateWeixinWorkApp).mockResolvedValue(undefined)
  vi.mocked(deleteWeixinWorkApp).mockResolvedValue(undefined)
})

describe('AppWeixinWorkChannelsSection（应用外部渠道·企业微信）', () => {
  it('只展示接入到当前应用的企业微信连接，含状态与接入指引', async () => {
    renderSection()
    expect(await screen.findByText('企微助手')).toBeInTheDocument()
    expect(screen.getByText('wwcorp1 / 1000002')).toBeInTheDocument()
    expect(screen.getByText('在线')).toBeInTheDocument()
    expect(screen.queryByText('闲置连接')).not.toBeInTheDocument()
    expect(screen.queryByText('其它应用渠道')).not.toBeInTheDocument()
    expect(screen.getByText('接入指引')).toBeInTheDocument()
    expect(screen.getByText('企业微信渠道')).toBeInTheDocument()
    expect(screen.queryByText(/应用尚未发布/)).not.toBeInTheDocument()
  })

  it('应用未发布时提示消息不会被处理', async () => {
    renderSection({ isPublished: false })
    expect(await screen.findByText(/应用尚未发布/)).toBeInTheDocument()
  })

  it('接入企业微信应用：创建连接即绑定到当前应用（单次调用）', async () => {
    renderSection()
    fireEvent.click(await screen.findByRole('button', { name: /接\s*入\s*企\s*业\s*微\s*信\s*应\s*用/ }))

    fireEvent.change(screen.getByLabelText('连接名称'), { target: { value: '新连接' } })
    fireEvent.change(screen.getByLabelText('企业 ID（CorpID）'), { target: { value: 'wwcorp-new' } })
    fireEvent.change(screen.getByLabelText('AgentId'), { target: { value: '1000099' } })
    fireEvent.change(screen.getByLabelText('Secret'), { target: { value: 'secret-1' } })
    fireEvent.change(screen.getByLabelText('回调 Token'), { target: { value: 'token-1' } })
    fireEvent.change(screen.getByLabelText('EncodingAESKey'), { target: { value: 'a'.repeat(43) } })
    fireEvent.click(screen.getByRole('button', { name: /^创\s*建$/ }))

    await waitFor(() =>
      expect(createWeixinWorkApp).toHaveBeenCalledWith({
        teamId: 3,
        name: '新连接',
        description: undefined,
        corpId: 'wwcorp-new',
        corpSecret: 'secret-1',
        agentId: 1000099,
        token: 'token-1',
        encodingAesKey: 'a'.repeat(43),
        apiEndpoint: undefined,
        channelType: 'app',
        channelId: 'a1',
      }),
    )
  })

  it('编辑：回显表单并展示回调地址，密钥留空表示保持不变（不改启停状态）', async () => {
    renderSection()
    fireEvent.click(await screen.findByRole('button', { name: /编\s*辑/ }))

    expect(await screen.findByText('编辑企业微信应用')).toBeInTheDocument()
    expect(screen.getByLabelText('连接名称')).toHaveValue('企微助手')
    expect(screen.getByLabelText('企业 ID（CorpID）')).toHaveValue('wwcorp1')
    expect(screen.getByDisplayValue(/api\/weixinwork_app\/w1\/callback/)).toBeInTheDocument()

    fireEvent.change(screen.getByLabelText('连接名称'), { target: { value: '改名连接' } })
    fireEvent.change(screen.getByLabelText('企业 ID（CorpID）'), { target: { value: 'wwcorp-fixed' } })
    // 密钥留空直接保存
    fireEvent.click(screen.getByRole('button', { name: /^保\s*存$/ }))

    await waitFor(() =>
      expect(updateWeixinWorkApp).toHaveBeenCalledWith('w1', {
        name: '改名连接',
        description: undefined,
        corpId: 'wwcorp-fixed',
        agentId: 1000002,
        corpSecret: undefined,
        token: undefined,
        encodingAesKey: undefined,
        apiEndpoint: undefined,
        isDisable: false,
      }),
    )
  })

  it('编辑：填写新 Secret 一并保存', async () => {
    renderSection()
    fireEvent.click(await screen.findByRole('button', { name: /编\s*辑/ }))
    await screen.findByText('编辑企业微信应用')

    fireEvent.change(screen.getByLabelText('Secret'), { target: { value: 'new-secret' } })
    fireEvent.click(screen.getByRole('button', { name: /^保\s*存$/ }))

    await waitFor(() =>
      expect(updateWeixinWorkApp).toHaveBeenCalledWith('w1', expect.objectContaining({ corpSecret: 'new-secret' })),
    )
  })

  it('停用需二次确认，启用直接执行', async () => {
    renderSection()
    fireEvent.click(await screen.findByRole('button', { name: /停\s*用/ }))
    fireEvent.click(await screen.findByRole('button', { name: /确\s*定/ }))
    await waitFor(() =>
      expect(updateWeixinWorkApp).toHaveBeenCalledWith('w1', expect.objectContaining({ isDisable: true })),
    )
  })

  it('删除需二次确认', async () => {
    renderSection()
    fireEvent.click(await screen.findByRole('button', { name: /删\s*除/ }))
    fireEvent.click(await screen.findByRole('button', { name: /确\s*定/ }))
    await waitFor(() => expect(deleteWeixinWorkApp).toHaveBeenCalledWith('w1'))
  })

  it('非管理员只读：无操作列与工具栏按钮', async () => {
    renderSection({ canManage: false })
    expect(await screen.findByText('企微助手')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /接\s*入\s*企\s*业\s*微\s*信\s*应\s*用/ })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /停\s*用/ })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /删\s*除/ })).not.toBeInTheDocument()
  })
})

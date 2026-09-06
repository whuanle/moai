import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { TeamGateway } from '../TeamGateway'
import {
  createTeamApiKey,
  deleteTeamApiKey,
  getTeamGatewayModels,
  updateTeamApiKey,
} from '@/api/gateway'

vi.mock('@/api/gateway', () => ({
  createTeamApiKey: vi.fn().mockResolvedValue({ apiKeyId: 'k1', secret: 'moai-test-secret-123', keyPrefix: 'moai-test' }),
  deleteTeamApiKey: vi.fn().mockResolvedValue(undefined),
  getTeamApiKeys: vi.fn().mockResolvedValue([
    {
      id: 'k1',
      name: '默认密钥',
      keyPrefix: 'moai-Ab12CdEf',
      isDisable: false,
      isExpired: false,
      expireTime: null,
      lastUsedTime: '2026-09-06T10:00:00Z',
      createTime: '2026-09-01T00:00:00Z',
    },
  ]),
  getTeamGatewayModels: vi.fn().mockResolvedValue([
    {
      aiModelId: 'm1',
      name: 'GPT-4o',
      modelId: 'gpt-4o',
      channelName: 'OpenAI 渠道',
      providerKey: 'openai',
      quota: { periodValue: 1, periodUnit: 2, limitValue: '1000000', usedTokens: '200000', periodEnd: null },
      totalUsedTokens: '50000',
    },
  ]),
  updateTeamApiKey: vi.fn().mockResolvedValue(undefined),
}))

function renderGateway(canManage = true) {
  return render(<TeamGateway teamId={7} canManage={canManage} />)
}

describe('TeamGateway', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('渲染接入说明、可用模型与额度', async () => {
    renderGateway(false)
    expect(await screen.findByText('模型网关接入')).toBeInTheDocument()
    expect(await screen.findByText(/Base URL:/)).toBeInTheDocument()
    expect(await screen.findByText('GPT-4o')).toBeInTheDocument()
    expect(screen.getByText('gpt-4o')).toBeInTheDocument()
    expect(screen.getByText(/200,000/)).toBeInTheDocument()
    expect(screen.getByText(/每1天|每天/)).toBeInTheDocument()
    // 非管理员看不到密钥区块
    expect(screen.queryByText('API 密钥')).not.toBeInTheDocument()
  })

  it('管理员可见密钥列表并可禁用', async () => {
    renderGateway(true)
    expect(await screen.findByText('API 密钥')).toBeInTheDocument()
    expect(await screen.findByText('默认密钥')).toBeInTheDocument()
    expect(screen.getByText('moai-Ab12CdEf')).toBeInTheDocument()

    fireEvent.click(screen.getByLabelText('禁用'))
    await waitFor(() => {
      expect(updateTeamApiKey).toHaveBeenCalledWith(7, 'k1', { isDisable: true })
    })
  })

  it('创建密钥后展示仅此一次的密钥原文', async () => {
    renderGateway(true)
    fireEvent.click(await screen.findByText('创建密钥'))
    fireEvent.change(await screen.findByPlaceholderText('请输入密钥名称'), { target: { value: 'CI 集成' } })
    // Modal 默认 footer 的确认键（antd 默认英文为 OK）
    fireEvent.click(await screen.findByRole('button', { name: 'OK' }))

    await waitFor(() => {
      expect(createTeamApiKey).toHaveBeenCalledWith(7, { name: 'CI 集成' })
    })
    expect(await screen.findByText('密钥创建成功')).toBeInTheDocument()
    expect(screen.getByText('moai-test-secret-123')).toBeInTheDocument()
  })

  it('删除密钥需确认且调用删除接口', async () => {
    renderGateway(true)
    fireEvent.click(await screen.findByText('删除'))
    // Popconfirm 确认按钮（未包 AppProviders 时显示为 OK）
    fireEvent.click(await screen.findByRole('button', { name: 'OK' }))
    await waitFor(() => {
      expect(deleteTeamApiKey).toHaveBeenCalledWith(7, 'k1')
    })
  })

  it('加载失败不抛出未处理异常', async () => {
    ;(getTeamGatewayModels as ReturnType<typeof vi.fn>).mockRejectedValueOnce(new Error('boom'))
    renderGateway(false)
    await waitFor(() => {
      expect(getTeamGatewayModels).toHaveBeenCalled()
    })
  })
})

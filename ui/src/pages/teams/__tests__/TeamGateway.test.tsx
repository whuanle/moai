import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { TeamGateway } from '../TeamGateway'
import { getTeamGatewayModels } from '@/api/gateway'

vi.mock('@/api/gateway', () => ({
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
}))

function renderGateway() {
  return render(<TeamGateway teamId={7} />)
}

describe('TeamGateway', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('渲染接入说明与 Base URL', async () => {
    renderGateway()
    expect(await screen.findByText('模型网关接入')).toBeInTheDocument()
    expect(await screen.findByText(/Base URL:/)).toBeInTheDocument()
    // 密钥管理已下线，页面不再出现创建密钥入口
    expect(screen.queryByText('API 密钥')).not.toBeInTheDocument()
    expect(screen.queryByText('创建密钥')).not.toBeInTheDocument()
  })

  it('渲染可用模型与额度', async () => {
    renderGateway()
    expect(await screen.findByText('GPT-4o')).toBeInTheDocument()
    expect(screen.getByText('gpt-4o')).toBeInTheDocument()
    expect(screen.getByText(/200,000/)).toBeInTheDocument()
    expect(screen.getByText(/每1天|每天/)).toBeInTheDocument()
  })

  it('刷新按钮重新加载模型列表', async () => {
    renderGateway()
    await screen.findByText('GPT-4o')
    fireEvent.click(screen.getByText('刷新'))
    await waitFor(() => {
      expect(getTeamGatewayModels).toHaveBeenCalledTimes(2)
    })
  })

  it('加载失败不抛出未处理异常', async () => {
    ;(getTeamGatewayModels as ReturnType<typeof vi.fn>).mockRejectedValueOnce(new Error('boom'))
    renderGateway()
    await waitFor(() => {
      expect(getTeamGatewayModels).toHaveBeenCalled()
    })
  })
})

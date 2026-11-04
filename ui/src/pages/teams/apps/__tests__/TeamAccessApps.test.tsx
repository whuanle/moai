import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { TeamAccessApps } from '../TeamAccessApps'
import { createAccessApp, getAccessApps, resetAccessAppSecret } from '@/api/access-app'

vi.mock('@/api/access-app', () => ({
  getAccessApps: vi.fn(),
  createAccessApp: vi.fn().mockResolvedValue({ accessAppId: 'x', key: 'moai-ac-secret', secret: 's'.repeat(48), keyPrefix: 'moai-ac-abc' }),
  updateAccessApp: vi.fn().mockResolvedValue(undefined),
  deleteAccessApp: vi.fn().mockResolvedValue(undefined),
  resetAccessAppSecret: vi.fn(),
  getAppFeature: vi.fn().mockResolvedValue({ isAcpEnabled: false, isA2aEnabled: false }),
  updateAppFeature: vi.fn().mockResolvedValue({ isAcpEnabled: false, isA2aEnabled: false }),
}))

describe('TeamAccessApps（团队应用接入分区）', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(getAccessApps).mockResolvedValue({
      teamId: 1,
      myRole: 1,
      items: [
        {
          accessAppId: '01924f5e-0000-7000-8000-0000000000c1',
          name: 'ERP 接入',
          description: 'erp 系统',
          key: 'moai-ac-abcd1234efgh5678ijkl',
          createTime: '2026-09-10T02:00:00Z',
        },
      ],
    })
  })

  it('加载并展示接入列表，key 默认掩码可点击查看', async () => {
    render(<TeamAccessApps teamId={1} canManage />)

    expect(await screen.findByText('ERP 接入')).toBeTruthy()
    expect(screen.getByText('moai-ac-abcd****')).toBeTruthy()
    expect(screen.getByText(/全量访问团队 \/api\/teamres 资源/)).toBeTruthy()
    await waitFor(() => expect(getAccessApps).toHaveBeenCalledWith(1))

    fireEvent.click(screen.getByRole('button', { name: '点击查看完整 key' }))
    expect(await screen.findByText('moai-ac-abcd1234efgh5678ijkl')).toBeTruthy()
  })

  it('管理员可新建接入（无需勾选范围）并在创建后显示一次性 key', async () => {
    render(<TeamAccessApps teamId={1} canManage />)
    await screen.findByText('ERP 接入')

    fireEvent.click(screen.getByRole('button', { name: /新建接入/ }))
    fireEvent.change(await screen.findByPlaceholderText('请输入接入名称'), { target: { value: '官网接入' } })
    fireEvent.click(screen.getByRole('button', { name: 'OK' }))

    await waitFor(() =>
      expect(createAccessApp).toHaveBeenCalledWith(
        expect.objectContaining({ teamId: 1, name: '官网接入' }),
      ),
    )
    expect(await screen.findByText('moai-ac-secret')).toBeTruthy()
  })

  it('创建后同时展示 appid 与一次性密钥', async () => {
    render(<TeamAccessApps teamId={1} canManage />)
    await screen.findByText('ERP 接入')

    fireEvent.click(screen.getByRole('button', { name: /新建接入/ }))
    fireEvent.change(await screen.findByPlaceholderText('请输入接入名称'), { target: { value: '官网接入' } })
    fireEvent.click(screen.getByRole('button', { name: 'OK' }))

    expect(await screen.findByText('s'.repeat(48))).toBeTruthy()
    expect(screen.getByText(/密钥（仅本次显示/)).toBeTruthy()
  })

  it('重置密钥后一次性展示新密钥', async () => {
    vi.mocked(resetAccessAppSecret).mockResolvedValue('r'.repeat(48))
    render(<TeamAccessApps teamId={1} canManage />)
    await screen.findByText('ERP 接入')

    fireEvent.click(screen.getByRole('button', { name: /重\s*置\s*密\s*钥/ }))
    fireEvent.click(await screen.findByRole('button', { name: /确 定|OK|确定/ }))

    expect(await screen.findByText('r'.repeat(48))).toBeTruthy()
    expect(resetAccessAppSecret).toHaveBeenCalledWith('01924f5e-0000-7000-8000-0000000000c1')
  })

  it('普通成员无管理入口', async () => {
    render(<TeamAccessApps teamId={1} canManage={false} />)

    expect(screen.getByText('只有团队管理员可以查看应用接入')).toBeTruthy()
    expect(screen.queryByRole('button', { name: /新建接入/ })).toBeNull()
  })

  it('接入页不出现资源 key 生成入口（资源 key 在各资源功能页自动生成）', async () => {
    render(<TeamAccessApps teamId={1} canManage />)
    await screen.findByText('ERP 接入')

    expect(screen.queryByText(/应用资源 Key/)).toBeNull()
    expect(screen.queryByRole('button', { name: /生成 Key/ })).toBeNull()
  })
})

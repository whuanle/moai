import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { TeamAccessApps } from '../TeamAccessApps'
import { createAccessApp, getAccessApps } from '@/api/access-app'

vi.mock('@/api/access-app', () => ({
  getAccessApps: vi.fn(),
  createAccessApp: vi.fn().mockResolvedValue({ accessAppId: 'x', key: 'moai-ac-secret', keyPrefix: 'moai-ac-abc' }),
  updateAccessApp: vi.fn().mockResolvedValue(undefined),
  deleteAccessApp: vi.fn().mockResolvedValue(undefined),
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
    expect(screen.getByText(/第三方系统可通过应用接入 key/)).toBeTruthy()
    await waitFor(() => expect(getAccessApps).toHaveBeenCalledWith(1))

    fireEvent.click(screen.getByRole('button', { name: '点击查看完整 key' }))
    expect(await screen.findByText('moai-ac-abcd1234efgh5678ijkl')).toBeTruthy()
  })

  it('管理员可新建接入并在创建后显示一次性 key', async () => {
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

  it('普通成员无管理入口', async () => {
    render(<TeamAccessApps teamId={1} canManage={false} />)

    expect(screen.getByText('只有团队管理员可以查看应用接入')).toBeTruthy()
    expect(screen.queryByRole('button', { name: /新建接入/ })).toBeNull()
  })
})

import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router'
import { TeamExternalApps } from '../TeamExternalApps'
import { createApp, getExternalApps } from '@/api/app'

vi.mock('@/api/app', () => ({
  getExternalApps: vi.fn(),
  createApp: vi.fn().mockResolvedValue('01924f5e-0000-7000-8000-0000000000aa'),
  publishApp: vi.fn().mockResolvedValue(undefined),
  unpublishApp: vi.fn().mockResolvedValue(undefined),
}))

function renderSection(canManage: boolean) {
  return render(
    <MemoryRouter initialEntries={['/team/1/externalApps']}>
      <Routes>
        <Route path="/team/:teamId/externalApps" element={<TeamExternalApps teamId={1} canManage={canManage} />} />
        <Route path="/team/:teamId/app/:appId" element={<div>应用管理页</div>} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('TeamExternalApps（团队外部应用分区）', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(getExternalApps).mockResolvedValue({
      teamId: 1,
      myRole: 1,
      items: [
        {
          appId: '01924f5e-0000-7000-8000-0000000000e1',
          teamId: 1,
          name: '对外助手',
          description: '挂到官网',
          appType: 'agent',
          avatarPath: '',
          isExternal: true,
          isAuth: true,
          publishStatus: 1,
          createTime: '2026-09-10T02:00:00Z',
        },
        {
          appId: '01924f5e-0000-7000-8000-0000000000e2',
          teamId: 1,
          name: '匿名问答',
          description: '',
          appType: 'agent',
          avatarPath: '',
          isExternal: true,
          isAuth: false,
          publishStatus: 0,
          createTime: '2026-09-10T03:00:00Z',
        },
      ],
    })
  })

  it('按团队加载并以卡片展示外部应用与授权状态', async () => {
    renderSection(true)

    expect(await screen.findByText('对外助手')).toBeTruthy()
    expect(screen.getByText('匿名问答')).toBeTruthy()
    expect(screen.getByText('需授权')).toBeTruthy()
    expect(screen.getByText('免授权')).toBeTruthy()
    await waitFor(() => expect(getExternalApps).toHaveBeenCalledWith(1))
  })

  it('管理员可见新建与发布入口', async () => {
    renderSection(true)
    await screen.findByText('对外助手')

    expect(screen.getByRole('button', { name: /新建外部应用/ })).toBeTruthy()
    expect(screen.getByRole('button', { name: /取消发布/ })).toBeTruthy()
    expect(screen.getByRole('button', { name: /发布/ })).toBeTruthy()
  })

  it('创建外部应用时带上 isExternal', async () => {
    renderSection(true)
    await screen.findByText('对外助手')

    fireEvent.click(screen.getByRole('button', { name: /新建外部应用/ }))
    await screen.findByText('需要授权访问')

    fireEvent.change(screen.getByPlaceholderText('请输入应用名称'), { target: { value: '新外部应用' } })
    fireEvent.click(screen.getByRole('button', { name: /确\s*定/ }))

    await waitFor(() =>
      expect(createApp).toHaveBeenCalledWith(
        expect.objectContaining({ teamId: 1, name: '新外部应用', isExternal: true }),
      ),
    )
  })

  it('普通成员无新建入口', async () => {
    renderSection(false)
    await screen.findByText('对外助手')

    expect(screen.queryByRole('button', { name: /新建外部应用/ })).toBeNull()
  })
})

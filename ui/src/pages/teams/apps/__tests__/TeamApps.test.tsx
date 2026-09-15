import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router'
import { TeamApps } from '../TeamApps'
import { createApp, getApps } from '@/api/app'

vi.mock('@/api/app', () => ({
  getApps: vi.fn(),
  createApp: vi.fn().mockResolvedValue('01924f5e-0000-7000-8000-000000000003'),
  updateApp: vi.fn().mockResolvedValue(undefined),
  uploadAppAvatar: vi.fn().mockResolvedValue(''),
}))

function renderSection(canManage: boolean) {
  return render(
    <MemoryRouter initialEntries={['/team/1/apps']}>
      <Routes>
        <Route path="/team/:teamId/apps" element={<TeamApps teamId={1} canManage={canManage} />} />
        <Route path="/team/:teamId/app/:appId" element={<div>应用管理页</div>} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('TeamApps（团队内应用分区，卡片展示）', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    // vi.clearAllMocks 不会重置 mock 实现，这里显式复位，避免跨用例泄漏
    vi.mocked(getApps).mockResolvedValue({
      teamId: 1,
      myRole: 1,
      items: [
        {
          appId: '01924f5e-0000-7000-8000-000000000001',
          teamId: 1,
          name: '客服助手',
          description: '售前售后问答',
          appType: 'agent',
          avatarPath: '',
          isPublic: true,
          createTime: '2026-09-10T02:00:00Z',
        },
        {
          appId: '01924f5e-0000-7000-8000-000000000002',
          teamId: 1,
          name: '审批流',
          description: '',
          appType: 'workflow',
          avatarPath: '',
          isPublic: false,
          createTime: '2026-09-10T03:00:00Z',
        },
      ],
    })
  })

  it('按团队查询并以卡片展示应用名称与类型', async () => {
    renderSection(true)

    expect(await screen.findByText('客服助手')).toBeTruthy()
    expect(screen.getByText('审批流')).toBeTruthy()
    expect(screen.getByText('Agent 应用')).toBeTruthy()
    expect(screen.getByText('流程应用')).toBeTruthy()
    await waitFor(() => expect(getApps).toHaveBeenCalledWith(1))
  })

  it('团队管理员可见新建入口与卡片右上角「管理」', async () => {
    renderSection(true)

    expect(await screen.findByText('客服助手')).toBeTruthy()
    expect(screen.getAllByRole('button', { name: /管\s*理/ }).length).toBe(2)
    expect(screen.getByRole('button', { name: /新建应用/ })).toBeTruthy()
  })

  it('点击卡片「管理」进入应用管理页', async () => {
    renderSection(true)

    fireEvent.click((await screen.findAllByRole('button', { name: /管\s*理/ }))[0])

    expect(await screen.findByText('应用管理页')).toBeTruthy()
  })

  it('普通成员只能查看使用：无新建与「管理」入口', async () => {
    renderSection(false)

    expect(await screen.findByText('客服助手')).toBeTruthy()
    expect(screen.queryByRole('button', { name: /管\s*理/ })).toBeNull()
    expect(screen.queryByRole('button', { name: /新建应用/ })).toBeNull()
    expect(screen.getByText(/创建与配置需要团队管理员/)).toBeTruthy()
  })

  it('卡片展示「公开到平台」状态', async () => {
    renderSection(true)

    expect(await screen.findByText('客服助手')).toBeTruthy()
    expect(screen.getByText('已公开')).toBeTruthy()
    expect(screen.getByText('未公开')).toBeTruthy()
  })

  it('新建弹窗可设置头像，提交创建请求（公开需走上架审核，弹窗不再提供公开开关）', async () => {
    renderSection(true)
    await screen.findByText('客服助手')

    fireEvent.click(screen.getByRole('button', { name: /新建应用/ }))

    expect(await screen.findByRole('button', { name: /上传头像/ })).toBeTruthy()
    expect(screen.queryByText('公开到平台')).toBeNull()

    fireEvent.change(screen.getByPlaceholderText('请输入应用名称'), { target: { value: '新助手' } })
    fireEvent.click(screen.getByRole('button', { name: /确\s*定/ }))

    await waitFor(() =>
      expect(createApp).toHaveBeenCalledWith(
        expect.objectContaining({ teamId: 1, name: '新助手' }),
      ),
    )
  })
})

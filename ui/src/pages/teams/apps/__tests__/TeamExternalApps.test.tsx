import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router'
import { TeamExternalApps } from '../TeamExternalApps'
import { createApp, getExternalApps, publishApp, unpublishApp } from '@/api/app'

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

  it('按团队加载并以卡片展示外部应用（is_auth 已退役，不再展示授权状态标签）', async () => {
    renderSection(true)

    expect(await screen.findByText('对外助手')).toBeTruthy()
    expect(screen.getByText('匿名问答')).toBeTruthy()
    expect(screen.queryByText('需授权')).toBeNull()
    expect(screen.queryByText('免授权')).toBeNull()
    await waitFor(() => expect(getExternalApps).toHaveBeenCalledWith(1))
  })

  it('管理员可见新建入口与发布开关（管理按钮下方）', async () => {
    renderSection(true)
    await screen.findByText('对外助手')

    expect(screen.getByRole('button', { name: /新建外部应用/ })).toBeTruthy()
    // 发布状态改用卡片开关：两张卡片各一个
    const switches = screen.getAllByRole('switch')
    expect(switches.length).toBe(2)
    expect(screen.getAllByRole('button', { name: /管\s*理/ }).length).toBe(2)
  })

  it('停用已发布应用需二次确认，确认后调用停用接口', async () => {
    renderSection(true)
    await screen.findByText('对外助手')

    const switches = screen.getAllByRole('switch')
    // 第一张卡（对外助手）已发布，点击开关弹出停用确认
    fireEvent.click(switches[0])
    expect(await screen.findByText(/确定停用该应用/)).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: /^确\s*定$/ }))
    await waitFor(() =>
      expect(unpublishApp).toHaveBeenCalledWith('01924f5e-0000-7000-8000-0000000000e1'),
    )
  })

  it('未发布应用打开开关直接发布，无需确认', async () => {
    renderSection(true)
    await screen.findByText('匿名问答')

    const switches = screen.getAllByRole('switch')
    fireEvent.click(switches[1])
    await waitFor(() =>
      expect(publishApp).toHaveBeenCalledWith('01924f5e-0000-7000-8000-0000000000e2'),
    )
    expect(screen.queryByText(/确定停用该应用/)).toBeNull()
  })

  it('创建外部应用时带上 isExternal（is_auth 已退役，创建弹窗无授权开关）', async () => {
    renderSection(true)
    await screen.findByText('对外助手')

    fireEvent.click(screen.getByRole('button', { name: /新建外部应用/ }))
    await screen.findByPlaceholderText('请输入应用名称')
    expect(screen.queryByText('需要授权访问')).toBeNull()

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

  it('关键字搜索过滤卡片', async () => {
    renderSection(true)
    await screen.findByText('对外助手')

    fireEvent.change(screen.getByPlaceholderText('搜索应用名称/描述'), { target: { value: '对外' } })
    // antd 对两字中文按钮自动插空格：「查询」的可访问名为「loading查 询」（含 loading 图标），用尾部锚定正则避免误匹配
    fireEvent.click(screen.getByRole('button', { name: /查\s*询$/ }))

    expect(screen.getByText('对外助手')).toBeTruthy()
    expect(screen.queryByText('匿名问答')).toBeNull()
  })
})

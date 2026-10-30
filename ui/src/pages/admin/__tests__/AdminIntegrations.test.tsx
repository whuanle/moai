import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { AdminIntegrations } from '../AdminIntegrations'
import { useAppStore } from '@/store/app'
import { getSystemAccessApps, createSystemAccessApp, updateSystemAccessApp, deleteSystemAccessApp, resetSystemAccessAppSecret } from '@/api/systemAccessApp'

vi.mock('@/api/subsystem', () => ({
  getSubsystems: vi.fn().mockResolvedValue([
    {
      subsystemId: 'sub-1',
      key: 'bi',
      name: 'MoAI BI',
      description: '',
      status: 'enabled',
      teamScope: 'allTeams',
      authorizedTeamCount: 0,
      online: true,
      callbackUrl: '',
    },
  ]),
  createSubsystem: vi.fn(),
  updateSubsystem: vi.fn(),
  deleteSubsystem: vi.fn(),
  resetSubsystemSecret: vi.fn(),
  getSubsystemAuthorizedTeams: vi.fn().mockResolvedValue({ teamIds: [] }),
}))

vi.mock('@/api/team', () => ({
  getAdminTeams: vi.fn().mockResolvedValue({ totalCount: 0, items: [] }),
}))

vi.mock('@/api/systemAccessApp', () => ({
  getSystemAccessApps: vi.fn(),
  createSystemAccessApp: vi.fn(),
  updateSystemAccessApp: vi.fn(),
  deleteSystemAccessApp: vi.fn(),
  resetSystemAccessAppSecret: vi.fn(),
}))

function renderPage(initialEntries = ['/admin/integrations']) {
  return render(
    <MemoryRouter initialEntries={initialEntries}>
      <AdminIntegrations />
    </MemoryRouter>,
  )
}

describe('AdminIntegrations（应用接入中心）', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    useAppStore.setState({
      userInfo: { accessToken: 'token', userId: '1', userName: 'admin', isAdmin: true, isRoot: true },
    })
    vi.mocked(getSystemAccessApps).mockResolvedValue([
      {
        id: 'app-1',
        name: 'ERP 组织同步',
        description: '同步 ERP 组织角色',
        keyPrefix: 'moai-sa-abcd',
        scopes: ['org_read', 'org_write'],
        status: 'enabled',
        createTime: '2026-10-28T10:00:00Z',
      },
    ])
  })

  it('非 root 跳转应用广场', () => {
    useAppStore.setState({ userInfo: { accessToken: 'token', userId: '2', userName: 'u', isAdmin: false, isRoot: false } })
    renderPage()
    expect(screen.queryByRole('tab', { name: '子系统管理' })).not.toBeInTheDocument()
  })

  it('默认展示子系统管理 Tab 与子系统数据', async () => {
    renderPage()
    expect(screen.getByRole('tab', { name: '子系统管理' })).toBeInTheDocument()
    expect(screen.getByRole('tab', { name: '应用接入' })).toBeInTheDocument()
    expect(await screen.findByText('MoAI BI')).toBeInTheDocument()
  })

  it('切换到应用接入 Tab 展示系统接入列表', async () => {
    renderPage()
    await screen.findByText('MoAI BI')
    await userEvent.click(screen.getByRole('tab', { name: '应用接入' }))

    expect(await screen.findByText('ERP 组织同步')).toBeInTheDocument()
    expect(screen.getByText('moai-sa-abcd')).toBeInTheDocument()
    expect(screen.getByText('组织读')).toBeInTheDocument()
    expect(screen.getByText('组织写')).toBeInTheDocument()
  })

  it('URL 带 tab=apps 时直达应用接入', async () => {
    renderPage(['/admin/integrations?tab=apps'])
    expect(await screen.findByText('ERP 组织同步')).toBeInTheDocument()
  })

  it('停用走更新接口', async () => {
    renderPage(['/admin/integrations?tab=apps'])
    await screen.findByText('ERP 组织同步')
    await userEvent.click(screen.getByRole('button', { name: /停用|Disable/ }))
    await userEvent.click(screen.getByRole('button', { name: /确 定|OK|确定/ }))

    await waitFor(() =>
      expect(updateSystemAccessApp).toHaveBeenCalledWith('app-1', expect.objectContaining({ status: 'disabled' })))
  })
})

describe('SystemAccessAppsPanel 创建', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    useAppStore.setState({
      userInfo: { accessToken: 'token', userId: '1', userName: 'admin', isAdmin: true, isRoot: true },
    })
    vi.mocked(getSystemAccessApps).mockResolvedValue([])
    vi.mocked(createSystemAccessApp).mockResolvedValue({ id: 'app-2', key: 'moai-sa-newkey', keyPrefix: 'moai-sa-new' })
  })

  it('创建成功展示一次性 key', async () => {
    renderPage(['/admin/integrations?tab=apps'])
    await userEvent.click(screen.getByRole('button', { name: /新建应用接入/ }))
    await userEvent.type(screen.getByLabelText(/接入名称/), 'ERP')
    // 组织架构选「只读」提供 scope
    const radios = screen.getAllByRole('radio', { name: /只读|Read/ })
    await userEvent.click(radios[0])
    await userEvent.click(screen.getByRole('button', { name: /确 定|OK|确定/ }))

    expect(await screen.findByText(/仅显示一次/)).toBeInTheDocument()
    expect(screen.getByText('moai-sa-newkey')).toBeInTheDocument()
    expect(createSystemAccessApp).toHaveBeenCalledWith(expect.objectContaining({ name: 'ERP', scopes: ['org_read'] }))
  })
})

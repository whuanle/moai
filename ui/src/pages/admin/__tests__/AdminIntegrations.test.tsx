import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import '@/i18n'
import { AdminIntegrations } from '../AdminIntegrations'
import { useAppStore } from '@/store/app'
import { getSubsysApps } from '@/api/subsystem'

vi.mock('@/api/subsystem', () => ({
  getSubsysApps: vi.fn().mockResolvedValue([
    {
      id: 'sub-1',
      appType: 'subsystem',
      key: 'bi',
      keyPrefix: 'bi',
      name: 'MoAI BI',
      description: '',
      status: 'enabled',
      teamScope: 'allTeams',
      online: true,
      onlineInstances: 2,
      callbackUrl: '',
    },
    {
      id: 'app-1',
      appType: 'app',
      key: 'moai-sa-abcd1234',
      keyPrefix: 'moai-sa-abcd',
      name: 'ERP 组织同步',
      description: '',
      status: 'enabled',
      teamScope: 'allTeams',
      online: false,
      onlineInstances: 0,
      callbackUrl: '',
    },
  ]),
  createSubsysApp: vi.fn(),
  updateSubsysApp: vi.fn(),
  deleteSubsysApp: vi.fn(),
  resetSubsysAppSecret: vi.fn(),
  getSubsystemAuthorizedTeams: vi.fn().mockResolvedValue([]),
  getSubsystemOrgGrants: vi.fn().mockResolvedValue([]),
  setSubsystemOrgGrants: vi.fn(),
}))

vi.mock('@/api/team', () => ({
  getAdminTeams: vi.fn().mockResolvedValue({ totalCount: 0, items: [] }),
}))

vi.mock('@/api/org', () => ({
  getOrgRoles: vi.fn().mockResolvedValue([]),
}))

function renderPage() {
  return render(
    <MemoryRouter>
      <AdminIntegrations />
    </MemoryRouter>,
  )
}

describe('AdminIntegrations（应用接入中心，子系统与应用接入单列表）', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    useAppStore.setState({
      userInfo: { accessToken: 'token', userId: '1', userName: 'admin', isAdmin: true, isRoot: true },
    })
  })

  it('非 root 跳转应用广场', () => {
    useAppStore.setState({ userInfo: { accessToken: 'token', userId: '2', userName: 'u', isAdmin: false, isRoot: false } })
    renderPage()
    expect(screen.queryByText('MoAI BI')).not.toBeInTheDocument()
  })

  it('子系统与应用接入同一列表展示（类型列区分）', async () => {
    renderPage()
    expect(await screen.findByText('MoAI BI')).toBeInTheDocument()
    expect(screen.getByText('ERP 组织同步')).toBeInTheDocument()
    expect(screen.getByText('子系统')).toBeInTheDocument()
    expect(screen.getByText('应用接入')).toBeInTheDocument()
    // 子系统行显示在线（含实例数），应用接入行在线列显示 -
    expect(screen.getByText('在线（2 实例）')).toBeInTheDocument()
    expect(getSubsysApps).toHaveBeenCalled()
  })

  it('不再渲染 Tabs（两个 Tab 已合并）', async () => {
    renderPage()
    await screen.findByText('MoAI BI')
    expect(screen.queryByRole('tab')).not.toBeInTheDocument()
  })
})

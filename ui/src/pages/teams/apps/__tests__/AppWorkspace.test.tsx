import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router'
import { AppWorkspace } from '../AppWorkspace'
import { getAppDetail } from '@/api/app'

vi.mock('@/api/app', () => ({
  getAppDetail: vi.fn(),
  getAppAgentConfig: vi.fn().mockResolvedValue({ appId: 'a1', appType: 'agent', prompt: '', modelId: null, wikiIds: [], plugins: [] }),
  saveAppAgentConfig: vi.fn(),
  updateApp: vi.fn(),
  uploadAppAvatar: vi.fn(),
  publishApp: vi.fn(),
  unpublishApp: vi.fn(),
  createDebugSession: vi.fn().mockResolvedValue('0198f2c1-1111-7000-8000-000000000001'),
}))
vi.mock('@/api/gateway', () => ({ getTeamGatewayModels: vi.fn().mockResolvedValue([]) }))
vi.mock('@/api/team-plugin', () => ({ getTeamPlugins: vi.fn().mockResolvedValue({ items: [] }) }))
vi.mock('@/api/wiki', () => ({ getWikis: vi.fn().mockResolvedValue({ items: [] }) }))
vi.mock('@/api/agentChat', () => ({
  createAppChatAgent: vi.fn(() => ({})),
  runAppChat: vi.fn().mockResolvedValue(undefined),
  abortAppChat: vi.fn(),
}))

function renderPage(path = '/team/3/app/a1') {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route path="/team/:teamId/app/:appId/:section?" element={<AppWorkspace />} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('AppWorkspace（应用工作台）', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(getAppDetail).mockResolvedValue({
      appId: 'a1',
      teamId: '3',
      name: '客服助手',
      appType: 'agent',
      avatarPath: '',
      isExternal: false,
      publishStatus: 0,
      myRole: 2,
    } as never)
  })

  it('内部应用展示配置/日志/监控三个菜单项，无访问点', async () => {
    renderPage()
    expect(await screen.findByRole('menuitem', { name: /配置/ })).toBeTruthy()
    expect(screen.getByRole('menuitem', { name: /日志/ })).toBeTruthy()
    expect(screen.getByRole('menuitem', { name: /监控/ })).toBeTruthy()
    expect(screen.queryByRole('menuitem', { name: /访问点/ })).toBeNull()
  })

  it('外部应用额外展示访问点菜单项', async () => {
    vi.mocked(getAppDetail).mockResolvedValue({
      appId: 'a2',
      teamId: '3',
      name: '外部客服',
      appType: 'agent',
      avatarPath: '',
      isExternal: true,
      publishStatus: 0,
      myRole: 2,
    } as never)

    renderPage('/team/3/app/a2')

    expect(await screen.findByRole('menuitem', { name: /访问点/ })).toBeTruthy()
  })

  it('Member 只看到配置菜单项，无调试对话', async () => {
    vi.mocked(getAppDetail).mockResolvedValue({
      appId: 'a1',
      teamId: '3',
      name: '客服助手',
      appType: 'agent',
      avatarPath: '',
      isExternal: false,
      publishStatus: 1,
      myRole: 0,
    } as never)

    renderPage()

    expect(await screen.findByRole('menuitem', { name: /配置/ })).toBeTruthy()
    await waitFor(() => expect(screen.queryByRole('menuitem', { name: /日志/ })).toBeNull())
    expect(screen.getByText(/调试需要团队管理员权限/)).toBeTruthy()
  })
})

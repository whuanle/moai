import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import '@/i18n'
import { AccessAppsPanel } from '../AdminAccessApps'
import { useAppStore } from '@/store/app'
import {
  createSubsysApp,
  deleteSubsysApp,
  getSubsystemAuthorizedTeams,
  getSubsystemOrgGrants,
  getSubsysApps,
  resetSubsysAppSecret,
  revealSubsysAppSecret,
  setSubsystemOrgGrants,
  updateSubsysApp,
} from '@/api/subsystem'

vi.mock('@/api/subsystem', () => ({
  getSubsysApps: vi.fn(),
  createSubsysApp: vi.fn().mockResolvedValue({ id: 'app-2', appType: 'app', key: 'moai-sa-newkey123456', keyPrefix: 'moai-sa-new', secretMask: 'moai****once' }),
  updateSubsysApp: vi.fn().mockResolvedValue(undefined),
  deleteSubsysApp: vi.fn().mockResolvedValue(undefined),
  resetSubsysAppSecret: vi.fn().mockResolvedValue({ secretMask: 'neww****1234' }),
  revealSubsysAppSecret: vi.fn().mockResolvedValue('S1cretOnlyOnce'),
  getSubsystemAuthorizedTeams: vi.fn().mockResolvedValue([2]),
  getSubsystemOrgGrants: vi.fn().mockResolvedValue([{ orgRoleId: 5, roleName: '经理', rolePath: '/5/', scope: 'down' }]),
  setSubsystemOrgGrants: vi.fn().mockResolvedValue(undefined),
}))

vi.mock('@/api/org', () => ({
  getOrgRoles: vi.fn().mockResolvedValue([{ orgRoleId: 5, name: '经理', parentId: null, path: '/5/', userCount: 2, hasChildren: false, isDefault: false }]),
}))

vi.mock('@/api/team', () => ({
  getAdminTeams: vi.fn().mockResolvedValue({
    totalCount: 2,
    items: [
      { teamId: 1, name: '研发团队' },
      { teamId: 2, name: '运营团队' },
    ],
  }),
}))

function renderPage() {
  return render(
    <MemoryRouter>
      <AccessAppsPanel />
    </MemoryRouter>,
  )
}

describe('AccessAppsPanel（子系统与应用接入合并列表）', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    useAppStore.setState({
      userInfo: {
        accessToken: 'token',
        userId: '1',
        userName: 'admin',
        isAdmin: true,
        isRoot: true,
      },
    })
    vi.mocked(getSubsysApps).mockResolvedValue([
      {
        id: 'sub-1',
        appType: 'subsystem',
        key: 'report',
        keyPrefix: 'report',
        name: '智能报表',
        description: '报表子系统',
        status: 'enabled',
        teamScope: 'allTeams',
        authorizedTeamCount: 0,
        online: true,
        onlineInstances: 3,
        createTime: '2026-10-01T00:00:00Z',
        createUserName: 'admin',
      },
      {
        id: 'app-1',
        appType: 'app',
        key: 'moai-sa-x1QuMJPj',
        keyPrefix: 'moai-sa-x1Qu',
        name: 'ERP 组织同步',
        description: '',
        status: 'disabled',
        teamScope: 'allTeams',
        online: false,
        onlineInstances: 0,
        createTime: '2026-10-02T00:00:00Z',
        createUserName: 'admin',
      },
    ])
  })

  it('加载并展示合并列表（类型/appid/授权范围/在线）', async () => {
    renderPage()
    expect(await screen.findByText('report')).toBeTruthy()
    expect(screen.getByText('moai-sa-x1Qu')).toBeTruthy()
    expect(screen.getByText('全员可用')).toBeTruthy()
    expect(screen.getByText('在线（3 实例）')).toBeTruthy()
    // 应用接入行在线列显示 -
    expect(screen.getAllByText('-').length).toBeGreaterThan(0)
  })

  it('新建应用接入：自动生成 appid，无需资源范围，创建后信封解密展示密钥', async () => {
    renderPage()
    fireEvent.click(await screen.findByRole('button', { name: /新\s*建接入/ }))

    // 类型切到应用接入：appid 输入框消失（自动生成）
    const appRadio = await screen.findByRole('radio', { name: '应用接入' })
    fireEvent.click(appRadio)
    expect(screen.queryByPlaceholderText('如 report，创建后不可修改')).not.toBeInTheDocument()

    fireEvent.change(await screen.findByPlaceholderText('请输入接入名称'), { target: { value: 'ERP' } })
    fireEvent.click(screen.getByRole('button', { name: /保\s*存/ }))

    await waitFor(() =>
      expect(createSubsysApp).toHaveBeenCalledWith(
        expect.objectContaining({ appType: 'app', name: 'ERP' }),
      ),
    )
    // 自动信封解密展示一次原文，appid 完整展示可复制
    expect(await screen.findByText('S1cretOnlyOnce')).toBeTruthy()
    expect(await screen.findByText('moai-sa-newkey123456')).toBeTruthy()
    expect(revealSubsysAppSecret).toHaveBeenCalledWith('app-2')
    // 应用接入不写组织授权
    expect(setSubsystemOrgGrants).not.toHaveBeenCalled()
  })

  it('新建子系统：自填 appid 提交后信封解密展示密钥', async () => {
    vi.mocked(createSubsysApp).mockResolvedValueOnce({ id: 'sub-2', appType: 'subsystem', key: 'report2', keyPrefix: 'report2', secretMask: 'repo****once' })
    vi.mocked(revealSubsysAppSecret).mockResolvedValueOnce('SubSecretOnce')
    renderPage()
    fireEvent.click(await screen.findByRole('button', { name: /新\s*建接入/ }))

    fireEvent.change(await screen.findByPlaceholderText('如 report，创建后不可修改'), { target: { value: 'report2' } })
    fireEvent.change(await screen.findByPlaceholderText('请输入接入名称'), { target: { value: '新报表' } })
    fireEvent.click(screen.getByRole('button', { name: /保\s*存/ }))

    await waitFor(() =>
      expect(createSubsysApp).toHaveBeenCalledWith(
        expect.objectContaining({ appType: 'subsystem', key: 'report2', name: '新报表' }),
      ),
    )
    expect(await screen.findByText('SubSecretOnce')).toBeTruthy()
  })

  it('编辑指定团队子系统：回显已授权团队并整体覆盖保存', async () => {
    vi.mocked(getSubsysApps).mockResolvedValue([
      {
        id: 'sub-2',
        appType: 'subsystem',
        key: 'bi',
        keyPrefix: 'bi',
        name: 'BI 看板',
        description: '',
        status: 'disabled',
        teamScope: 'specifiedTeams',
        authorizedTeamCount: 1,
        authorizedTeams: [{ teamId: 2, teamName: '运营团队' }],
        online: false,
        onlineInstances: 0,
        callbackUrls: ['https://bi.local/cb'],
        createTime: '2026-10-02T00:00:00Z',
        createUserName: 'admin',
      },
    ])
    renderPage()
    const rows = await waitFor(() => {
      const list = Array.from(document.querySelectorAll('table tr')).filter((r) => r.textContent?.includes('bi'))
      expect(list.length).toBeGreaterThan(0)
      return list
    })
    fireEvent.click(withinRowButton(rows[0], '编辑'))

    await waitFor(() => expect(getSubsystemAuthorizedTeams).toHaveBeenCalledWith('sub-2'))
    fireEvent.click(screen.getByRole('button', { name: /保\s*存/ }))
    await waitFor(() =>
      expect(updateSubsysApp).toHaveBeenCalledWith(
        'sub-2',
        expect.objectContaining({ status: 'disabled', teamScope: 'specifiedTeams', teamIds: [2] }),
      ),
    )
    // 组织授权编辑回显并整体覆盖保存
    await waitFor(() => expect(getSubsystemOrgGrants).toHaveBeenCalledWith('sub-2'))
    await waitFor(() => expect(setSubsystemOrgGrants).toHaveBeenCalledWith('sub-2', [{ orgRoleId: 5, scope: 'down' }]))
  })

  it('停用应用接入行：更新不携带子系统专属字段', async () => {
    renderPage()
    const rows = await waitFor(() => {
      const list = Array.from(document.querySelectorAll('table tr')).filter((r) => r.textContent?.includes('ERP'))
      expect(list.length).toBeGreaterThan(0)
      return list
    })
    fireEvent.click(withinRowButton(rows[0], '启用'))
    fireEvent.click(await screen.findByRole('button', { name: /确\s*定|OK|确\s*认/ }))
    await waitFor(() =>
      expect(updateSubsysApp).toHaveBeenCalledWith(
        'app-1',
        expect.objectContaining({ status: 'enabled', teamScope: undefined, callbackUrls: undefined }),
      ),
    )
  })

  it('重置密钥：确认后信封解密展示新密钥', async () => {
    vi.mocked(revealSubsysAppSecret).mockResolvedValueOnce('NewSecret123')
    renderPage()
    const rows = await waitFor(() => {
      const list = Array.from(document.querySelectorAll('table tr')).filter((r) => r.textContent?.includes('report'))
      expect(list.length).toBeGreaterThan(0)
      return list
    })
    fireEvent.click(withinRowButton(rows[0], '重置密钥'))
    fireEvent.click(await screen.findByRole('button', { name: /确\s*定|OK|确\s*认/ }))
    await waitFor(() => expect(resetSubsysAppSecret).toHaveBeenCalledWith('sub-1'))
    expect(await screen.findByText('NewSecret123')).toBeTruthy()
  })

  it('删除需 Popconfirm 二次确认', async () => {
    renderPage()
    const rows = await waitFor(() => {
      const list = Array.from(document.querySelectorAll('table tr')).filter((r) => r.textContent?.includes('ERP'))
      expect(list.length).toBeGreaterThan(0)
      return list
    })
    fireEvent.click(withinRowButton(rows[0], '删除'))
    fireEvent.click(await screen.findByRole('button', { name: /确\s*定|OK|确\s*认/ }))
    await waitFor(() => expect(deleteSubsysApp).toHaveBeenCalledWith('app-1'))
  })
})

/** 在表格行内按可见文字（或 aria-label）找操作按钮 */
function withinRowButton(row: Element, label: string): HTMLElement {
  const btn = Array.from(row.querySelectorAll('button')).find(
    (b) => (b.textContent ?? '').trim() === label || b.getAttribute('aria-label') === label,
  )
  expect(btn, `row button ${label} should exist`).toBeTruthy()
  return btn as HTMLElement
}

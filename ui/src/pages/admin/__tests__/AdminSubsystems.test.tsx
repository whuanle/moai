import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import '@/i18n'
import { SubsystemsPanel } from '../AdminSubsystems'
import { useAppStore } from '@/store/app'
import {
  createSubsystem,
  deleteSubsystem,
  getSubsystemAuthorizedTeams,
  getSubsystems,
  resetSubsystemSecret,
  updateSubsystem,
} from '@/api/subsystem'

vi.mock('@/api/subsystem', () => ({
  getSubsystems: vi.fn(),
  createSubsystem: vi.fn().mockResolvedValue({ subsystemId: 'sub-1', secret: 'S1cretOnlyOnce' }),
  updateSubsystem: vi.fn().mockResolvedValue(undefined),
  deleteSubsystem: vi.fn().mockResolvedValue(undefined),
  resetSubsystemSecret: vi.fn().mockResolvedValue({ secret: 'NewSecret123' }),
  getSubsystemAuthorizedTeams: vi.fn().mockResolvedValue([2]),
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
      <SubsystemsPanel />
    </MemoryRouter>,
  )
}

describe('SubsystemsPanel', () => {
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
    vi.mocked(getSubsystems).mockResolvedValue([
      {
        subsystemId: 'sub-1',
        key: 'report',
        name: '智能报表',
        description: '报表子系统',
        status: 'enabled',
        teamScope: 'allTeams',
        authorizedTeamCount: 0,
        online: true,
        createTime: '2026-10-01T00:00:00Z',
        createUserName: 'admin',
      },
      {
        subsystemId: 'sub-2',
        key: 'bi',
        name: 'BI 看板',
        description: '',
        status: 'disabled',
        teamScope: 'specifiedTeams',
        authorizedTeamCount: 1,
        authorizedTeams: [{ teamId: 2, teamName: '运营团队' }],
        online: false,
        createTime: '2026-10-02T00:00:00Z',
        createUserName: 'admin',
      },
    ])
  })


  it('加载并展示子系统列表（标识/授权范围/在线状态）', async () => {
    renderPage()
    expect(await screen.findByText('report')).toBeTruthy()
    expect(screen.getByText('全员可用')).toBeTruthy()
    // 指定团队范围展示完整授权团队名称清单
    expect(screen.getByText('运营团队')).toBeTruthy()
    expect((await screen.findAllByText('在线')).length).toBeGreaterThan(0)
    expect((await screen.findAllByText('离线')).length).toBeGreaterThan(0)
  })

  it('新建子系统：填表提交后弹出一次性密钥弹窗', async () => {
    renderPage()
    fireEvent.click(await screen.findByRole('button', { name: /新\s*建子系统/ }))

    fireEvent.change(await screen.findByPlaceholderText('如 report，创建后不可修改'), { target: { value: 'report2' } })
    fireEvent.change(await screen.findByPlaceholderText('请输入子系统名称'), { target: { value: '新报表' } })

    fireEvent.click(screen.getByRole('button', { name: /保\s*存/ }))
    await waitFor(() =>
      expect(createSubsystem).toHaveBeenCalledWith(
        expect.objectContaining({ key: 'report2', name: '新报表', teamScope: 'allTeams' }),
      ),
    )

    expect(await screen.findByText('S1cretOnlyOnce')).toBeTruthy()
    expect(await screen.findByText(/密钥仅在此时显示一次/)).toBeTruthy()
  })

  it('编辑指定团队子系统：回显已授权团队并整体覆盖保存', async () => {
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
      expect(updateSubsystem).toHaveBeenCalledWith(
        'sub-2',
        expect.objectContaining({ status: 'disabled', teamScope: 'specifiedTeams', teamIds: [2] }),
      ),
    )
  })

  it('重置密钥：确认后展示新密钥', async () => {
    renderPage()
    const rows = await waitFor(() => {
      const list = Array.from(document.querySelectorAll('table tr')).filter((r) => r.textContent?.includes('report'))
      expect(list.length).toBeGreaterThan(0)
      return list
    })
    fireEvent.click(withinRowButton(rows[0], '重置密钥'))
    fireEvent.click(await screen.findByRole('button', { name: /确\s*定|OK|确\s*认/ }))
    await waitFor(() => expect(resetSubsystemSecret).toHaveBeenCalledWith('sub-1'))
    expect(await screen.findByText('NewSecret123')).toBeTruthy()
  })

  it('删除子系统需 Popconfirm 二次确认', async () => {
    renderPage()
    const rows = await waitFor(() => {
      const list = Array.from(document.querySelectorAll('table tr')).filter((r) => r.textContent?.includes('bi'))
      expect(list.length).toBeGreaterThan(0)
      return list
    })
    fireEvent.click(withinRowButton(rows[0], '删除'))
    fireEvent.click(await screen.findByRole('button', { name: /确\s*定|OK|确\s*认/ }))
    await waitFor(() => expect(deleteSubsystem).toHaveBeenCalledWith('sub-2'))
  })
})

/** 在表格行内按 aria-label 找 icon 按钮 */
function withinRowButton(row: Element, label: string): HTMLElement {
  const btn = Array.from(row.querySelectorAll('button')).find((b) => b.getAttribute('aria-label') === label)
  expect(btn, `row button ${label} should exist`).toBeTruthy()
  return btn as HTMLElement
}

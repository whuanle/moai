import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { AdminTeams } from '../AdminTeams'
import { useAppStore } from '@/store/app'
import { getAdminTeams } from '@/api/team'

vi.mock('@/api/team', () => ({
  getAdminTeams: vi.fn().mockResolvedValue({
    totalCount: 2,
    items: [
      {
        teamId: 1,
        name: '研发团队',
        description: '核心研发',
        isDisable: false,
        memberCount: 3,
        ownerUserId: 11,
        ownerUserName: 'alice',
        ownerNickName: 'Alice',
        createTime: '2026-09-01T00:00:00Z',
      },
      {
        teamId: 2,
        name: '测试团队',
        description: '质量保障',
        isDisable: true,
        memberCount: 1,
        ownerUserId: 12,
        ownerUserName: 'bob',
        ownerNickName: 'Bob',
        createTime: '2026-09-02T00:00:00Z',
      },
    ],
  }),
  setTeamDisable: vi.fn().mockResolvedValue(undefined),
  adminTransferTeamOwner: vi.fn().mockResolvedValue(undefined),
}))

vi.mock('@/api/usermanage', () => ({
  getUsers: vi.fn().mockResolvedValue({
    totalCount: 1,
    items: [{ id: 21, userName: 'carol', nickName: 'Carol', email: 'carol@moai.com' }],
  }),
}))

function renderPage() {
  return render(
    <MemoryRouter>
      <AdminTeams />
    </MemoryRouter>,
  )
}

describe('AdminTeams', () => {
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
  })

  it('管理员看到全部团队的名称、负责人与状态', async () => {
    renderPage()

    await waitFor(() => {
      expect(getAdminTeams).toHaveBeenCalled()
    })
    expect(await screen.findByText('研发团队')).toBeInTheDocument()
    expect(screen.getByText('测试团队')).toBeInTheDocument()
    expect(screen.getByText('Alice')).toBeInTheDocument()
    expect(screen.getByText('Bob')).toBeInTheDocument()
    expect(screen.getByText('正常')).toBeInTheDocument()
    expect(screen.getByText('已禁用')).toBeInTheDocument()
  })

  it('禁用态与正常态渲染对应的操作按钮', async () => {
    renderPage()
    expect(await screen.findByText('研发团队')).toBeInTheDocument()

    const findRow = (text: string) =>
      Array.from(document.querySelectorAll('table tr')).find((r) => r.textContent?.includes(text))

    const actionLabels = (text: string) =>
      Array.from(findRow(text)?.querySelectorAll('button') ?? [])
        .map((b) => b.getAttribute('aria-label'))
        .filter(Boolean)

    // 正常团队：可禁用 + 可转让负责人
    const normalRow = actionLabels('研发团队')
    expect(normalRow).toContain('禁用')
    expect(normalRow).toContain('转让负责人')

    // 已禁用团队：可启用 + 可转让负责人
    const disabledRow = actionLabels('测试团队')
    expect(disabledRow).toContain('启用')
    expect(disabledRow).toContain('转让负责人')
  })

  it('非管理员访问重定向到 dashboard', () => {
    useAppStore.setState({ userInfo: { accessToken: 'token', userId: '2', isAdmin: false } })
    renderPage()
    expect(document.querySelector('table')).toBeNull()
  })
})

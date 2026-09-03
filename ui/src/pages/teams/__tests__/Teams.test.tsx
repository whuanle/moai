import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { Teams } from '../Teams'
import { useAppStore } from '@/store/app'
import { getMyTeams } from '@/api/team'

vi.mock('@/api/team', () => ({
  createTeam: vi.fn().mockResolvedValue(9),
  getMyTeams: vi.fn().mockResolvedValue([
    {
      teamId: '7',
      name: 'Alpha 团队',
      description: '第一个',
      myRole: 0,
      memberCount: 3,
      createTime: '2026-09-02T00:00:00Z',
    },
    {
      teamId: '8',
      name: 'Beta 团队',
      description: '第二个',
      myRole: 2,
      memberCount: 2,
      createTime: '2026-09-02T00:00:00Z',
    },
  ]),
  getTeamUsers: vi.fn().mockResolvedValue([
    { userId: '1', userName: 'owner', nickName: 'O', role: 0, joinTime: '2026-09-02T00:00:00Z' },
    { userId: '2', userName: 'member', nickName: 'M', role: 2, joinTime: '2026-09-02T00:00:00Z' },
  ]),
  getTeamDetail: vi.fn(),
  updateTeam: vi.fn().mockResolvedValue(undefined),
  dissolveTeam: vi.fn().mockResolvedValue(undefined),
  addTeamUser: vi.fn().mockResolvedValue(undefined),
  updateTeamUserRole: vi.fn().mockResolvedValue(undefined),
  removeTeamUser: vi.fn().mockResolvedValue(undefined),
}))

vi.mock('@/api/kiota', () => ({
  getApiClient: vi.fn(() => ({})),
}))

function renderTeams() {
  return render(
    <MemoryRouter>
      <Teams />
    </MemoryRouter>,
  )
}

describe('Teams', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    useAppStore.setState({
      userInfo: { accessToken: 'token', userId: '1', userName: 'owner', isAdmin: false },
    })
  })

  it('渲染我的团队列表与角色标签', async () => {
    renderTeams()

    await waitFor(() => {
      expect(getMyTeams).toHaveBeenCalled()
    })
    expect(await screen.findByText('Alpha 团队')).toBeInTheDocument()
    expect(screen.getByText('Beta 团队')).toBeInTheDocument()
    // Owner 标签（Alpha 行）+ 成员标签（Beta 行）
    expect(screen.getByText('所有者')).toBeInTheDocument()
    expect(screen.getByText('成员', { selector: '.ant-tag' })).toBeInTheDocument()
  })

  it('仅 Owner 行显示解散按钮', async () => {
    renderTeams()
    expect(await screen.findByText('Alpha 团队')).toBeInTheDocument()

    const findRow = (text: string) =>
      Array.from(document.querySelectorAll('table tr')).find((r) => r.textContent?.includes(text))

    expect(findRow('Alpha 团队')?.textContent).toContain('解散')
    expect(findRow('Beta 团队')?.textContent).not.toContain('解散')
  })

  it('成员弹窗展示成员列表，Owner 行不可移除', async () => {
    renderTeams()
    expect(await screen.findByText('Alpha 团队')).toBeInTheDocument()

    // 点击第一行（Alpha，Owner 团队）的成员按钮
    const row = Array.from(document.querySelectorAll('table tr')).find((r) =>
      r.textContent?.includes('Alpha 团队'),
    )
    const membersBtn = Array.from(row!.querySelectorAll('button')).find((b) => b.textContent === '成员')
    membersBtn!.click()

    expect(await screen.findByText('owner')).toBeInTheDocument()
    expect(screen.getByText('member')).toBeInTheDocument()
    // Owner 自己的行（role=owner）操作列渲染 "-"
    const ownerRow = Array.from(document.querySelectorAll('table tr')).find((r) =>
      r.textContent?.includes('owner'),
    )
    expect(ownerRow?.textContent).toContain('-')
    // Member 行有移除按钮
    const memberRow = Array.from(document.querySelectorAll('table tr')).find((r) =>
      r.textContent?.includes('member'),
    )
    expect(Array.from(memberRow!.querySelectorAll('button')).map((b) => b.textContent)).toContain('移除')
  })

  it('Admin+ 行有设置入口，Owner 行额外有转让按钮，Member 行没有', async () => {
    renderTeams()
    expect(await screen.findByText('Alpha 团队')).toBeInTheDocument()

    const findRow = (text: string) =>
      Array.from(document.querySelectorAll('table tr')).find((r) => r.textContent?.includes(text))

    // Alpha（我是 Owner）：设置 + 解散
    expect(findRow('Alpha 团队')?.textContent).toContain('设置')
    expect(findRow('Alpha 团队')?.textContent).toContain('解散')
    // Beta（我是 Member）：无设置/解散
    expect(findRow('Beta 团队')?.textContent).not.toContain('设置')
    expect(findRow('Beta 团队')?.textContent).not.toContain('解散')

    // 打开成员弹窗：Owner 看到转让按钮（非 Owner 行）
    const row = findRow('Alpha 团队')
    const membersBtn = Array.from(row!.querySelectorAll('button')).find((b) => b.textContent === '成员')
    membersBtn!.click()
    expect(await screen.findByText('member')).toBeInTheDocument()
    expect(screen.getByText('转让所有权')).toBeInTheDocument()
  })
})

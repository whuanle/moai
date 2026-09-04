import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
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
      myRole: 2,
      memberCount: 3,
      createTime: '2026-09-02T00:00:00Z',
    },
    {
      teamId: '8',
      name: 'Beta 团队',
      description: '第二个',
      myRole: 0,
      memberCount: 2,
      createTime: '2026-09-02T00:00:00Z',
    },
    {
      teamId: '9',
      name: 'Gamma 团队',
      description: '第三个',
      myRole: 1,
      memberCount: 4,
      createTime: '2026-09-02T00:00:00Z',
    },
  ]),
  getTeamUsers: vi.fn().mockResolvedValue([
    { userId: '1', userName: 'owner', nickName: 'O', role: 2, joinTime: '2026-09-02T00:00:00Z' },
    { userId: '2', userName: 'member', nickName: 'M', role: 0, joinTime: '2026-09-02T00:00:00Z' },
  ]),
  getTeamDetail: vi.fn(),
  updateTeam: vi.fn().mockResolvedValue(undefined),
  dissolveTeam: vi.fn().mockResolvedValue(undefined),
  addTeamUser: vi.fn().mockResolvedValue(undefined),
  updateTeamUserRole: vi.fn().mockResolvedValue(undefined),
  removeTeamUser: vi.fn().mockResolvedValue(undefined),
  getTeamCandidates: vi.fn().mockResolvedValue([]),
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

/** 在卡片网格中查找包含指定团队名的卡片根节点 */
function findCard(text: string) {
  return Array.from(document.querySelectorAll('.ant-card')).find((c) => c.textContent?.includes(text))
}

describe('Teams', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    useAppStore.setState({
      userInfo: { accessToken: 'token', userId: '1', userName: 'owner', isAdmin: false },
    })
  })

  it('以卡片块渲染我的团队与角色标签', async () => {
    renderTeams()

    await waitFor(() => {
      expect(getMyTeams).toHaveBeenCalled()
    })
    expect(await screen.findByText('Alpha 团队')).toBeInTheDocument()
    expect(screen.getByText('Beta 团队')).toBeInTheDocument()
    expect(screen.getByText('Gamma 团队')).toBeInTheDocument()
    // Owner 标签（Alpha 卡片）+ Admin 标签（Gamma 卡片）+ 成员标签（Beta 卡片）
    expect(screen.getByText('所有者')).toBeInTheDocument()
    expect(screen.getByText('管理员')).toBeInTheDocument()
    expect(screen.getByText('成员', { selector: '.ant-tag' })).toBeInTheDocument()
  })

  it('卡片上按角色显示操作按钮：Owner 显示设置+解散，Admin 显示设置，Member 无', async () => {
    renderTeams()
    expect(await screen.findByText('Alpha 团队')).toBeInTheDocument()

    // Alpha（Owner）
    expect(findCard('Alpha 团队')?.querySelector("button[aria-label='设置']")).not.toBeNull()
    expect(findCard('Alpha 团队')?.querySelector("button[aria-label='解散']")).not.toBeNull()
    // Gamma（Admin）：设置，无解散
    expect(findCard('Gamma 团队')?.querySelector("button[aria-label='设置']")).not.toBeNull()
    expect(findCard('Gamma 团队')?.querySelector("button[aria-label='解散']")).toBeNull()
    // Beta（Member）：无设置/解散
    expect(findCard('Beta 团队')?.querySelector("button[aria-label='设置']")).toBeNull()
    expect(findCard('Beta 团队')?.querySelector("button[aria-label='解散']")).toBeNull()
  })

  it('成员弹窗展示成员列表，Owner 行不可移除', async () => {
    renderTeams()
    expect(await screen.findByText('Alpha 团队')).toBeInTheDocument()

    const membersBtn = findCard('Alpha 团队')?.querySelector("button[aria-label='成员']")
    ;(membersBtn as HTMLElement | null)?.click()

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
    expect(memberRow!.querySelector("button[aria-label='移除']")).not.toBeNull()
  })

  it('Owner 卡片成员弹窗含转让按钮', async () => {
    renderTeams()
    expect(await screen.findByText('Alpha 团队')).toBeInTheDocument()

    const membersBtn = findCard('Alpha 团队')?.querySelector("button[aria-label='成员']")
    ;(membersBtn as HTMLElement | null)?.click()
    expect(await screen.findByText('member')).toBeInTheDocument()
    expect(screen.getByLabelText('转让所有权')).toBeInTheDocument()
  })

  it('默认显示全部已加入团队，筛选“我创建的”仅剩 Owner 卡，筛选“我管理的”仅剩 Admin 卡', async () => {
    renderTeams()
    expect(await screen.findByText('Alpha 团队')).toBeInTheDocument()
    expect(screen.getByText('Beta 团队')).toBeInTheDocument()
    expect(screen.getByText('Gamma 团队')).toBeInTheDocument()

    fireEvent.click(screen.getByText('我创建的'))
    await waitFor(() => {
      expect(screen.getByText('Alpha 团队')).toBeInTheDocument()
      expect(screen.queryByText('Beta 团队')).not.toBeInTheDocument()
      expect(screen.queryByText('Gamma 团队')).not.toBeInTheDocument()
    })

    fireEvent.click(screen.getByText('我管理的'))
    await waitFor(() => {
      expect(screen.getByText('Gamma 团队')).toBeInTheDocument()
      expect(screen.queryByText('Alpha 团队')).not.toBeInTheDocument()
      expect(screen.queryByText('Beta 团队')).not.toBeInTheDocument()
    })
  })

  it('搜索可按团队名过滤', async () => {
    renderTeams()
    expect(await screen.findByText('Alpha 团队')).toBeInTheDocument()

    fireEvent.change(screen.getByPlaceholderText('搜索团队名称或简介'), { target: { value: 'Beta' } })
    await waitFor(() => {
      expect(screen.getByText('Beta 团队')).toBeInTheDocument()
      expect(screen.queryByText('Alpha 团队')).not.toBeInTheDocument()
      expect(screen.queryByText('Gamma 团队')).not.toBeInTheDocument()
    })
  })
})

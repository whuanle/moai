import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { Teams } from '../Teams'
import { useAppStore } from '@/store/app'
import { getMyTeams } from '@/api/team'

vi.mock('@/api/team', () => ({
  createTeam: vi.fn().mockResolvedValue(9),
  getMyTeams: vi.fn().mockResolvedValue([    {
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

  it('卡片不显示任何操作按钮（成员/设置/解散），操作需进入团队后进行', async () => {
    renderTeams()
    expect(await screen.findByText('Alpha 团队')).toBeInTheDocument()

    for (const cardName of ['Alpha 团队', 'Beta 团队', 'Gamma 团队']) {
      const card = findCard(cardName)
      expect(card?.querySelector("button[aria-label='成员']")).toBeNull()
      expect(card?.querySelector("button[aria-label='设置']")).toBeNull()
      expect(card?.querySelector("button[aria-label='解散']")).toBeNull()
    }
    expect(screen.queryByLabelText('成员')).not.toBeInTheDocument()
    expect(screen.queryByLabelText('设置')).not.toBeInTheDocument()
    expect(screen.queryByLabelText('解散')).not.toBeInTheDocument()
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

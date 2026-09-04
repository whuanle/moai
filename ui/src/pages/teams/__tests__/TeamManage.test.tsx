import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router'
import { TeamManage } from '../TeamManage'
import { useAppStore } from '@/store/app'
import { getTeamDetail, getTeamUsers } from '@/api/team'
import { getVariables } from '@/api/variable'

vi.mock('@/api/variable', () => ({
  getVariables: vi.fn().mockResolvedValue({
    teamId: '7',
    myRole: 0,
    items: [
      { variableId: '1', key: 'API_KEY', name: 'default', isSecret: false, value: 'abc123', description: '', updateTime: '2026-09-02T00:00:00Z' },
    ],
  }),
}))

vi.mock('@/api/team', () => ({
  getTeamDetail: vi.fn().mockResolvedValue({
    teamId: '7',
    name: 'Alpha 团队',
    description: '第一个',
    myRole: 2,
    memberCount: 2,
    createTime: '2026-09-02T00:00:00Z',
    ownerUserId: '1',
    ownerUserName: 'owner',
    ownerNickName: 'O',
  }),
  getTeamUsers: vi.fn().mockResolvedValue([
    { userId: '1', userName: 'owner', nickName: 'O', role: 2, joinTime: '2026-09-02T00:00:00Z' },
    { userId: '2', userName: 'member', nickName: 'M', role: 0, joinTime: '2026-09-02T00:00:00Z' },
  ]),
  addTeamUser: vi.fn().mockResolvedValue(undefined),
  removeTeamUser: vi.fn().mockResolvedValue(undefined),
  transferTeamOwner: vi.fn().mockResolvedValue(undefined),
  updateTeam: vi.fn().mockResolvedValue(undefined),
  updateTeamUserRole: vi.fn().mockResolvedValue(undefined),
  uploadTeamAvatar: vi.fn().mockResolvedValue(''),
  getTeamCandidates: vi.fn().mockResolvedValue([]),
}))

vi.mock('@/api/kiota', () => ({
  getApiClient: vi.fn(() => ({})),
}))

function renderManage(teamId = '7') {
  return render(
    <MemoryRouter initialEntries={[`/team/${teamId}`]}>
      <Routes>
        <Route path="/team/:id" element={<TeamManage />} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('TeamManage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    useAppStore.setState({
      userInfo: { accessToken: 'token', userId: '1', userName: 'owner', isAdmin: false },
    })
  })

  it('默认展示团队信息：名称、负责人、角色、成员数', async () => {
    renderManage()

    await waitFor(() => {
      expect(getTeamDetail).toHaveBeenCalledWith(7)
    })
    expect((await screen.findAllByText('Alpha 团队')).length).toBeGreaterThan(0)
    // 负责人标签 + Owner 昵称
    expect(screen.getAllByText(/负责人|O/).length).toBeGreaterThan(0)
    // 我的角色标签
    expect(screen.getByText('所有者')).toBeInTheDocument()
  })

  it('切换到成员菜单后展示成员列表', async () => {
    renderManage()

    expect((await screen.findAllByText('Alpha 团队')).length).toBeGreaterThan(0)
    // 点击左侧菜单「成员管理」
    fireEvent.click(screen.getByText('成员管理'))

    expect(await screen.findByText('owner')).toBeInTheDocument()
    expect(screen.getByText('member')).toBeInTheDocument()
    expect(getTeamUsers).toHaveBeenCalledWith(7)
  })

  it('切换到设置菜单展示表单', async () => {
    renderManage()

    expect((await screen.findAllByText('Alpha 团队')).length).toBeGreaterThan(0)
    fireEvent.click(screen.getByText('设置'))

    expect(await screen.findByText('团队名称')).toBeInTheDocument()
  })

  it('知识库/插件菜单展示占位空态', async () => {
    renderManage()

    expect((await screen.findAllByText('Alpha 团队')).length).toBeGreaterThan(0)
    fireEvent.click(screen.getByText('知识库'))
    expect(await screen.findByText('知识库', { selector: '.ant-empty-description' })).toBeInTheDocument()

    fireEvent.click(screen.getByText('插件'))
    expect(await screen.findByText('插件', { selector: '.ant-empty-description' })).toBeInTheDocument()
  })

  it('环境变量菜单嵌入变量组件并按团队加载', async () => {
    renderManage()

    expect((await screen.findAllByText('Alpha 团队')).length).toBeGreaterThan(0)
    fireEvent.click(screen.getByText('环境变量'))

    expect(getVariables).toHaveBeenCalledWith(7, expect.objectContaining({ keyword: undefined, name: undefined }))
    expect(await screen.findByText('API_KEY')).toBeInTheDocument()
  })
})

import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router'
import { TeamManage } from '../TeamManage'
import { useAppStore } from '@/store/app'
import { dissolveTeam, getTeamDetail, getTeamUsers, updateTeamUserRole } from '@/api/team'
import { getVariables } from '@/api/variable'
import { getTeamPlugins } from '@/api/team-plugin'
import { getWikis } from '@/api/wiki'
import { getKnowledgeGraphs } from '@/api/knowledgeGraph'

vi.mock('@/api/knowledgeGraph', () => ({
  getKnowledgeGraphs: vi.fn().mockResolvedValue({ teamId: '7', myRole: 2, enabled: true, items: [] }),
  getKnowledgeGraphTemplates: vi.fn().mockResolvedValue([]),
  createKnowledgeGraph: vi.fn().mockResolvedValue(1),
  updateKnowledgeGraph: vi.fn().mockResolvedValue(undefined),
  deleteKnowledgeGraph: vi.fn().mockResolvedValue(undefined),
}))

vi.mock('@/api/wiki', () => ({
  getWikis: vi.fn().mockResolvedValue({ teamId: '7', myRole: 2, items: [] }),
  getWikiDetail: vi.fn(),
  createWiki: vi.fn().mockResolvedValue(1),
  updateWiki: vi.fn().mockResolvedValue(undefined),
  deleteWiki: vi.fn().mockResolvedValue(undefined),
}))

vi.mock('@/api/app', () => ({
  getApps: vi.fn().mockResolvedValue({ teamId: '7', myRole: 2, items: [] }),
  getExternalApps: vi.fn().mockResolvedValue({ teamId: '7', myRole: 2, items: [] }),
  getPublicApps: vi.fn().mockResolvedValue([]),
  createApp: vi.fn().mockResolvedValue('01924f5e-0000-7000-8000-0000000000ff'),
  updateApp: vi.fn().mockResolvedValue(undefined),
  publishApp: vi.fn().mockResolvedValue(undefined),
  unpublishApp: vi.fn().mockResolvedValue(undefined),
  uploadAppAvatar: vi.fn().mockResolvedValue(''),
}))

vi.mock('@/api/access-app', () => ({
  getAccessApps: vi.fn().mockResolvedValue({ teamId: '7', myRole: 2, items: [] }),
  createAccessApp: vi.fn().mockResolvedValue({}),
  updateAccessApp: vi.fn().mockResolvedValue(undefined),
  deleteAccessApp: vi.fn().mockResolvedValue(undefined),
}))

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
  dissolveTeam: vi.fn().mockResolvedValue(undefined),
  getMyTeams: vi.fn().mockResolvedValue([]),
}))

vi.mock('@/api/kiota', () => ({
  getApiClient: vi.fn(() => ({})),
}))

vi.mock('@/api/team-plugin', () => ({
  getTeamPlugins: vi.fn().mockResolvedValue({
    teamId: '7',
    myRole: 0,
    canManage: false,
    items: [],
  }),
  deleteTeamPlugin: vi.fn().mockResolvedValue(undefined),
  saveTeamDynamicPlugin: vi.fn().mockResolvedValue(undefined),
  saveTeamMcpPlugin: vi.fn().mockResolvedValue('x'),
  saveTeamOpenApiPlugin: vi.fn().mockResolvedValue('x'),
  getTeamPluginDetail: vi.fn().mockResolvedValue(null),
  getTeamPluginFunctions: vi.fn().mockResolvedValue([]),
  refreshTeamMcp: vi.fn().mockResolvedValue(undefined),
  preUploadTeamOpenApiFile: vi.fn().mockResolvedValue(null),
  runTeamPlugin: vi.fn().mockResolvedValue(null),
  getTeamDynamicTemplates: vi.fn().mockResolvedValue({ items: [] }),
}))

/** 默认团队详情（Owner）；用例可覆盖，beforeEach 会复位，避免 mock 实现跨用例泄漏 */
const ownerTeamDetail = {
  teamId: '7',
  name: 'Alpha 团队',
  description: '第一个',
  myRole: 2,
  memberCount: 2,
  createTime: '2026-09-02T00:00:00Z',
}

function renderManage(teamId = '7', section = 'info') {
  return render(
    <MemoryRouter initialEntries={[`/team/${teamId}/${section}`]}>
      <Routes>
        <Route path="/team/:id/:section?" element={<TeamManage />} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('TeamManage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    ;(getTeamDetail as ReturnType<typeof vi.fn>).mockResolvedValue(ownerTeamDetail)
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

  it('切换到知识图谱菜单后加载图谱列表并展示新建入口', async () => {
    renderManage('7', 'knowledgegraph')

    expect((await screen.findAllByText('Alpha 团队')).length).toBeGreaterThan(0)
    await waitFor(() => expect(getKnowledgeGraphs).toHaveBeenCalledWith(7))
    expect(screen.getByText('新建知识图谱')).toBeInTheDocument()
    expect(screen.getByText('知识图谱')).toBeInTheDocument()
  })

  it('角色列只展示角色标签，不出现角色下拉框', async () => {
    renderManage()

    fireEvent.click(await screen.findByText('成员管理'))

    expect(await screen.findByText('所有者')).toBeInTheDocument()
    expect(screen.getAllByText('成员').length).toBeGreaterThan(0)
    // 旧角色下拉框已移除，其「设为成员」选项文案不再出现
    expect(screen.queryByText('设为成员')).not.toBeInTheDocument()
  })

  it('普通成员行在操作列显示设为管理员图标，点击后调用角色更新', async () => {
    renderManage()

    fireEvent.click(await screen.findByText('成员管理'))

    fireEvent.click(await screen.findByRole('button', { name: '设为管理员' }))
    await waitFor(() => {
      expect(updateTeamUserRole).toHaveBeenCalledWith(7, 2, 1)
    })
  })

  it('管理员行在操作列显示取消管理员图标，点击后降级为成员', async () => {
    ;(getTeamUsers as ReturnType<typeof vi.fn>).mockResolvedValue([
      { userId: '1', userName: 'owner', nickName: 'O', role: 2, joinTime: '2026-09-02T00:00:00Z' },
      { userId: '3', userName: 'admin1', nickName: 'A', role: 1, joinTime: '2026-09-02T00:00:00Z' },
    ])
    renderManage()

    fireEvent.click(await screen.findByText('成员管理'))

    fireEvent.click(await screen.findByRole('button', { name: '取消管理员' }))
    await waitFor(() => {
      expect(updateTeamUserRole).toHaveBeenCalledWith(7, 3, 0)
    })
  })

  it('切换到设置菜单展示表单', async () => {
    renderManage()

    expect((await screen.findAllByText('Alpha 团队')).length).toBeGreaterThan(0)
    fireEvent.click(screen.getByText('设置'))

    expect(await screen.findByText('团队名称')).toBeInTheDocument()
  })

  it('设置菜单中 Owner 可解散团队并返回列表', async () => {
    renderManage()

    expect((await screen.findAllByText('Alpha 团队')).length).toBeGreaterThan(0)
    fireEvent.click(screen.getByText('设置'))

    // antd Button 会在两个汉字间自动插入空格，实际渲染为「解 散」
    fireEvent.click(await screen.findByText(/解\s*散/))
    // 测试环境未包裹 AppProviders，antd 默认英文 locale，Popconfirm 确认按钮为 OK
    fireEvent.click(screen.getByRole('button', { name: 'OK' }))

    await waitFor(() => {
      expect(dissolveTeam).toHaveBeenCalledWith(7)
    })
  })

  it('设置菜单中非 Owner 不显示解散按钮', async () => {
    ;(getTeamDetail as ReturnType<typeof vi.fn>).mockResolvedValue({
      teamId: '7',
      name: 'Alpha 团队',
      description: '第一个',
      myRole: 1,
      memberCount: 2,
      createTime: '2026-09-02T00:00:00Z',
    })
    renderManage()

    expect((await screen.findAllByText('Alpha 团队')).length).toBeGreaterThan(0)
    fireEvent.click(screen.getByText('设置'))

    expect(await screen.findByText('团队名称')).toBeInTheDocument()
    expect(screen.queryByText(/解\s*散/)).not.toBeInTheDocument()
  })

  it('知识库菜单嵌入团队知识库管理组件并按团队加载', async () => {
    renderManage()

    expect((await screen.findAllByText('Alpha 团队')).length).toBeGreaterThan(0)
    fireEvent.click(screen.getByText('知识库'))
    expect(getWikis).toHaveBeenCalledWith(7)
    expect(await screen.findByRole('button', { name: /新建知识库/ })).toBeInTheDocument()

    fireEvent.click(screen.getByText('插件'))
    expect(await screen.findByRole('tab', { name: '自定义插件' })).toBeInTheDocument()
    expect(screen.getByRole('tab', { name: '动态插件' })).toBeInTheDocument()
    expect(getTeamPlugins).toHaveBeenCalledWith(7)
  })

  it('环境变量菜单嵌入变量组件并按团队加载', async () => {
    renderManage()

    expect((await screen.findAllByText('Alpha 团队')).length).toBeGreaterThan(0)
    fireEvent.click(screen.getByText('环境变量'))

    expect(getVariables).toHaveBeenCalledWith(7, expect.objectContaining({ keyword: undefined, name: undefined }))
    expect(await screen.findByText('API_KEY')).toBeInTheDocument()
  })

  it('URL 中的子路由片段决定当前区块，刷新后停留在原页', async () => {
    renderManage(undefined, 'variables')

    expect((await screen.findAllByText('Alpha 团队')).length).toBeGreaterThan(0)
    expect(await screen.findByText('API_KEY')).toBeInTheDocument()
    expect(getVariables).toHaveBeenCalledWith(7, expect.objectContaining({ keyword: undefined, name: undefined }))
  })

  it('普通成员进入团队只能使用：只保留 信息/应用/知识库 分区', async () => {
    ;(getTeamDetail as ReturnType<typeof vi.fn>).mockResolvedValue({
      teamId: '7',
      name: 'Alpha 团队',
      description: '第一个',
      myRole: 0,
      memberCount: 2,
      createTime: '2026-09-02T00:00:00Z',
    })
    renderManage()

    expect((await screen.findAllByText('Alpha 团队')).length).toBeGreaterThan(0)
    expect(screen.getByText('内部应用')).toBeInTheDocument()
    expect(screen.getByText('知识库')).toBeInTheDocument()
    // 管理分区与应用配置入口对成员不可见
    expect(screen.queryByText('外部应用')).not.toBeInTheDocument()
    expect(screen.queryByText('应用接入')).not.toBeInTheDocument()
    expect(screen.queryByText('成员管理')).not.toBeInTheDocument()
    expect(screen.queryByText('模型网关')).not.toBeInTheDocument()
    expect(screen.queryByText('插件')).not.toBeInTheDocument()
    expect(screen.queryByText('环境变量')).not.toBeInTheDocument()
    expect(screen.queryByText('设置')).not.toBeInTheDocument()
  })

  it('普通成员直接访问管理分区的 URL 会回落到信息页', async () => {
    ;(getTeamDetail as ReturnType<typeof vi.fn>).mockResolvedValue({
      teamId: '7',
      name: 'Alpha 团队',
      description: '第一个',
      myRole: 0,
      memberCount: 2,
      createTime: '2026-09-02T00:00:00Z',
    })
    renderManage('7', 'variables')

    expect((await screen.findAllByText('Alpha 团队')).length).toBeGreaterThan(0)
    expect(await screen.findByText('负责人')).toBeInTheDocument()
    expect(getVariables).not.toHaveBeenCalled()
  })

  it('非法子路由片段回退到信息区块', async () => {
    renderManage(undefined, 'not-exist')

    expect((await screen.findAllByText('Alpha 团队')).length).toBeGreaterThan(0)
    expect(await screen.findByText('负责人')).toBeInTheDocument()
  })
})

import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { TeamPlugins } from '../TeamPlugins'
import { getTeamPlugins, getTeamPluginStats } from '@/api/team-plugin'
import type { PluginStatsData } from '@/api/plugin'
import { useAppStore } from '@/store/app'

vi.mock('@/api/team-plugin', () => ({
  getTeamPlugins: vi.fn(),
  getTeamPluginStats: vi.fn(),
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

vi.mock('@/api/classify', () => ({
  classifyApi: { getPluginClassifies: vi.fn().mockResolvedValue([]) },
  classifyLabel: (c: { name?: string | null }) => c.name ?? '',
}))

const CUSTOM_ITEM = {
  pluginId: 'p-custom',
  pluginName: 'weather',
  title: '天气',
  type: 'mcp' as const,
  kind: 'custom',
  isTeamOwned: true,
  isSystem: false,
  classifyId: 0,
  server: 'http://mcp.example.com',
  description: '查询天气',
  counter: '3',
  createUserName: 'owner',
  createTime: '2026-09-01T10:00:00',
  updateTime: '2026-09-01T10:00:00',
}

const DYNAMIC_ITEM = {
  pluginId: 'p-dynamic',
  pluginName: 'greet',
  title: '问候',
  type: 'nativePlugin' as const,
  kind: 'dynamic',
  isTeamOwned: true,
  isSystem: false,
  classifyId: 0,
  instanceKey: 'greet',
  templeteKey: 'dynamic_greet',
  config: '{}',
  counter: '0',
  createTime: '2026-09-02T10:00:00',
  updateTime: '2026-09-02T10:00:00',
}

const TEAM_STATS: PluginStatsData = {
  totalPlugins: 2,
  mcpCount: 1,
  openApiCount: 0,
  totalCalls: 3,
  totalFunctions: 4,
  top: [{ id: 'p-custom', name: '天气', counter: 3, avatarPath: null }],
  classifies: [{ classifyId: 0, classifyName: null, count: 2 }],
  trend: {
    items: [{ date: '2026-11-01', count: 0 }],
    total: 0,
    startDate: '2026-11-01',
    endDate: '2026-11-01',
  },
}

describe('TeamPlugins', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    useAppStore.setState({
      userInfo: { accessToken: 'token', userId: '1', userName: 'owner', isAdmin: false },
    })
    vi.mocked(getTeamPlugins).mockResolvedValue({
      teamId: '7',
      myRole: 0,
      canManage: true,
      items: [CUSTOM_ITEM, DYNAMIC_ITEM],
    })
    vi.mocked(getTeamPluginStats).mockResolvedValue(TEAM_STATS)
  })

  function renderTeamPlugins() {
    // TeamDynamicPluginPanel 的「模板列表」入口依赖 useNavigate
    return render(
      <MemoryRouter>
        <TeamPlugins teamId={7} />
      </MemoryRouter>,
    )
  }

  it('分为自定义与动态两个 tab', async () => {
    renderTeamPlugins()

    await waitFor(() => {
      expect(getTeamPlugins).toHaveBeenCalledWith(7)
    })
    expect(await screen.findByRole('tab', { name: '自定义插件' })).toBeInTheDocument()
    expect(screen.getByRole('tab', { name: '动态插件' })).toBeInTheDocument()
    expect(screen.getByRole('tab', { name: '统计' })).toBeInTheDocument()
  })

  it('统计 tab 展示本团队插件统计，TOP 名称不带团队后缀', async () => {
    const user = userEvent.setup()
    renderTeamPlugins()

    await screen.findByText('weather')
    await user.click(screen.getByRole('tab', { name: '统计' }))

    // 概览卡与 TOP5 来自 getTeamPluginStats
    expect(await screen.findByText('调用量 TOP 5')).toBeInTheDocument()
    expect(within(screen.getAllByRole('tabpanel').find((el) => !el.hidden) ?? document.body).getByText('天气')).toBeInTheDocument()
    // 本团队口径只显示插件名，不出现 {名称}-({团队名)} 后缀
    expect(screen.queryByText(/天气-\(/)).not.toBeInTheDocument()
    expect(getTeamPluginStats).toHaveBeenCalledWith(7)
  })

  it('自定义 tab 展示自定义插件，动态 tab 展示动态实例', async () => {
    const user = userEvent.setup()
    renderTeamPlugins()

    expect(await screen.findByText('weather')).toBeInTheDocument()
    expect(screen.queryByText('greet')).not.toBeInTheDocument()

    await user.click(screen.getByRole('tab', { name: '动态插件' }))
    expect(await screen.findByText('greet')).toBeInTheDocument()
  })

  it('可管理时展示导入/新建入口与视图切换（与系统插件页一致的 PageToolbar）', async () => {
    renderTeamPlugins()
    await screen.findByText('weather')

    expect(screen.getByRole('button', { name: /导入 MCP/ })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /导入 OpenAPI/ })).toBeInTheDocument()
    // 卡片/列表视图切换与刷新按钮
    expect(screen.getByRole('button', { name: /刷新/ })).toBeInTheDocument()
  })

  it('默认卡片视图渲染插件卡片，可切换到列表视图', async () => {
    const user = userEvent.setup()
    renderTeamPlugins()
    await screen.findByText('天气')

    // 卡片视图：描述与归属标签可见
    expect(screen.getByText('查询天气')).toBeInTheDocument()
    expect(screen.getAllByText('团队自有').length).toBeGreaterThan(0)

    // 切换列表视图：表格呈现（sticky 模式会渲染隐藏测量表，用 findAll）
    await user.click(screen.getByText('列表'))
    expect((await screen.findAllByRole('table')).length).toBeGreaterThan(0)
    expect(screen.getAllByText('服务器地址').length).toBeGreaterThan(0)
  })

  it('动态 tab 可管理时展示新建实例与模板列表入口', async () => {
    const user = userEvent.setup()
    renderTeamPlugins()
    await screen.findByText('weather')

    await user.click(screen.getByRole('tab', { name: '动态插件' }))
    expect(await screen.findByRole('button', { name: /新建实例/ })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /模板列表/ })).toBeInTheDocument()
  })

  it('非可管理成员不展示导入/新建/模板入口', async () => {
    vi.mocked(getTeamPlugins).mockResolvedValue({
      teamId: '7',
      myRole: 2,
      canManage: false,
      items: [CUSTOM_ITEM, DYNAMIC_ITEM],
    })
    const user = userEvent.setup()
    renderTeamPlugins()
    await screen.findByText('weather')

    expect(screen.queryByRole('button', { name: /导入 MCP/ })).not.toBeInTheDocument()
    await user.click(screen.getByRole('tab', { name: '动态插件' }))
    expect(screen.queryByRole('button', { name: /新建实例/ })).not.toBeInTheDocument()
  })

  it('系统授权插件只读：不展示编辑/删除，仅可查看函数', async () => {
    vi.mocked(getTeamPlugins).mockResolvedValue({
      teamId: '7',
      myRole: 0,
      canManage: true,
      items: [{ ...CUSTOM_ITEM, isTeamOwned: false }],
    })
    renderTeamPlugins()
    await screen.findByText('天气')

    expect(screen.queryByRole('button', { name: /编辑插件/ })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /删除插件/ })).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: /查看函数/ })).toBeInTheDocument()
    expect(screen.getAllByText('系统').length).toBeGreaterThan(0)
  })

  it('搜索关键字即时过滤插件卡片', async () => {
    const user = userEvent.setup()
    renderTeamPlugins()
    await screen.findByText('天气')

    await user.type(screen.getByPlaceholderText(/搜索/), 'no-such-plugin')
    await waitFor(() => {
      expect(screen.queryByText('查询天气')).not.toBeInTheDocument()
    })
  })

  it('动态实例引用的模板缺失时运行/编辑禁用并标记', async () => {
    const user = userEvent.setup()
    renderTeamPlugins()
    await screen.findByText('weather')

    await user.click(screen.getByRole('tab', { name: '动态插件' }))
    const card = await screen.findByText('greet').then((el) => el.closest('.ant-card'))
    expect(card).not.toBeNull()
    expect(within(card as HTMLElement).getByText('模板已下线')).toBeInTheDocument()
    const runBtn = within(card as HTMLElement).getByRole('button', { name: /运行/ })
    expect(runBtn).toBeDisabled()
  })
})

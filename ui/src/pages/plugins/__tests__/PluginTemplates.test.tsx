import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router'
import { PluginTemplates } from '../PluginTemplates'
import { useAppStore } from '@/store/app'
import { pluginApi } from '@/api/plugin'
import { getTeamDynamicTemplates, getTeamPlugins, saveTeamDynamicPlugin } from '@/api/team-plugin'

vi.mock('@/api/plugin', () => ({
  pluginApi: {
    getDynamicTemplates: vi.fn(),
    getManagePlugins: vi.fn(),
    saveDynamicPlugin: vi.fn(),
  },
}))

vi.mock('@/api/team-plugin', () => ({
  getTeamDynamicTemplates: vi.fn(),
  getTeamPlugins: vi.fn(),
  saveTeamDynamicPlugin: vi.fn(),
}))

vi.mock('@/api/classify', () => ({
  classifyApi: { getPluginClassifies: vi.fn().mockResolvedValue([]) },
  classifyLabel: (c: { name?: string | null }) => c.name ?? '',
}))

const MOCK_TEMPLATES = [
  { key: 'dynamic_greet', name: '动态问候', description: '问候插件模板', isDynamic: true, configExample: '{"Prefix":"Hello"}' },
  { key: 'postgres_query', name: 'Postgres 查询', description: '', isDynamic: true, configExample: '{}' },
]

function renderPage(initialEntry = '/plugin/templates') {
  return render(
    <MemoryRouter initialEntries={[initialEntry]}>
      <Routes>
        <Route path="/plugin" element={<div>动态插件页面标记</div>} />
        <Route path="/plugin/templates" element={<PluginTemplates />} />
        <Route path="/team/7/plugins" element={<div>团队插件页面标记</div>} />
        <Route path="/apps" element={<div>应用页面标记</div>} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('PluginTemplates（系统模式）', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    useAppStore.setState({
      userInfo: { accessToken: 'token', userId: '1', userName: 'admin', isAdmin: true },
    })
    vi.mocked(pluginApi.getDynamicTemplates).mockResolvedValue(MOCK_TEMPLATES)
    vi.mocked(pluginApi.getManagePlugins).mockResolvedValue([
      { id: 'i-1', pluginName: 'greet_cn', templeteKey: 'dynamic_greet', kind: 'dynamic' },
      { id: 'i-2', pluginName: 'greet_en', templeteKey: 'dynamic_greet', kind: 'dynamic' },
      { id: 'i-3', pluginName: 'pg_1', templeteKey: 'postgres_query', kind: 'dynamic' },
    ] as never)
    vi.mocked(pluginApi.saveDynamicPlugin).mockResolvedValue(null)
  })

  it('以卡片形式展示模板的 key、名称、描述与实例数', async () => {
    renderPage()

    expect(await screen.findByText('dynamic_greet')).toBeInTheDocument()
    expect(screen.getByText('动态问候')).toBeInTheDocument()
    expect(screen.getByText('问候插件模板')).toBeInTheDocument()
    // greet_cn/greet_en 两个实例同属 dynamic_greet 模板
    expect(screen.getByText('已有实例：2')).toBeInTheDocument()
    expect(screen.getByText('已有实例：1')).toBeInTheDocument()
    expect(screen.getByText('dynamic_greet').closest('.ant-card')).not.toBeNull()
  })

  it('描述为空时显示占位文案', async () => {
    renderPage()

    expect(await screen.findByText('暂无描述')).toBeInTheDocument()
  })

  it('模板为空时显示空状态', async () => {
    vi.mocked(pluginApi.getDynamicTemplates).mockResolvedValue([])
    vi.mocked(pluginApi.getManagePlugins).mockResolvedValue([])
    renderPage()

    expect(await screen.findByText('暂无模板')).toBeInTheDocument()
  })

  it('点击返回按钮回到动态插件页', async () => {
    const user = userEvent.setup()
    renderPage()
    await screen.findByText('dynamic_greet')

    await user.click(screen.getByRole('button', { name: /返回插件/ }))
    expect(await screen.findByText('动态插件页面标记')).toBeInTheDocument()
  })

  it('点击刷新重新加载模板', async () => {
    renderPage()
    await screen.findByText('dynamic_greet')

    const user = userEvent.setup()
    await user.click(screen.getByRole('button', { name: /刷新/ }))
    await screen.findByText('dynamic_greet')
    expect(pluginApi.getDynamicTemplates).toHaveBeenCalledTimes(2)
  })

  it('卡片「新建」打开创建模态并预选模板，提交走系统实例保存', async () => {
    const user = userEvent.setup()
    renderPage()
    await screen.findByText('dynamic_greet')

    const greetCard = screen.getByText('dynamic_greet').closest('.ant-card') as HTMLElement
    await user.click(within(greetCard).getByRole('button', { name: /新建/ }))
    expect(await screen.findByText('新建实例')).toBeInTheDocument()
    // 预选模板后下拉回显模板标签
    expect(await screen.findByText('动态问候 (dynamic_greet)')).toBeInTheDocument()

    await user.type(screen.getByLabelText('实例 Key'), 'my_greet')
    await user.type(screen.getByLabelText('插件标题'), '我的问候')
    await user.click(screen.getByRole('button', { name: /保 存|保存/ }))

    await waitFor(() => {
      expect(pluginApi.saveDynamicPlugin).toHaveBeenCalledWith(
        expect.objectContaining({ pluginKey: 'my_greet', templeteKey: 'dynamic_greet', title: '我的问候' }),
      )
    })
  })

  it('非管理员访问系统模式重定向到应用页', async () => {
    useAppStore.setState({
      userInfo: { accessToken: 'token', userId: '2', userName: 'user', isAdmin: false },
    })
    renderPage()

    expect(await screen.findByText('应用页面标记')).toBeInTheDocument()
  })
})

describe('PluginTemplates（团队模式）', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    useAppStore.setState({
      userInfo: { accessToken: 'token', userId: '2', userName: 'owner', isAdmin: false },
    })
    vi.mocked(getTeamDynamicTemplates).mockResolvedValue({
      items: [{ key: 'dynamic_greet', name: '动态问候', description: '', isDynamic: true, configExample: '{}' }],
    })
    vi.mocked(getTeamPlugins).mockResolvedValue({
      teamId: '7',
      myRole: 0,
      canManage: true,
      items: [
        // 团队自有动态实例：计入实例数
        { pluginId: 'p-1', pluginName: 'greet', instanceKey: 'greet', templeteKey: 'dynamic_greet', kind: 'dynamic', isTeamOwned: true },
        // 系统可用动态实例：不计入本团队实例数
        { pluginId: 'p-2', pluginName: 'sys_greet', instanceKey: 'sys_greet', templeteKey: 'dynamic_greet', kind: 'dynamic', isTeamOwned: false },
      ],
    } as never)
    vi.mocked(saveTeamDynamicPlugin).mockResolvedValue(null)
  })

  it('实例数只统计团队自有实例，提交走团队实例保存', async () => {
    const user = userEvent.setup()
    renderPage('/plugin/templates?teamId=7')
    await screen.findByText('dynamic_greet')

    expect(screen.getByText('已有实例：1')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: /新建/ }))
    expect(await screen.findByText('新建实例')).toBeInTheDocument()
    await user.type(screen.getByLabelText('实例 Key'), 'team_greet')
    await user.type(screen.getByLabelText('插件标题'), '团队问候')
    await user.click(screen.getByRole('button', { name: /保 存|保存/ }))

    await waitFor(() => {
      expect(saveTeamDynamicPlugin).toHaveBeenCalledWith(
        expect.objectContaining({ teamId: 7, instanceKey: 'team_greet', templeteKey: 'dynamic_greet' }),
      )
    })
    expect(pluginApi.saveDynamicPlugin).not.toHaveBeenCalled()
  })

  it('返回按钮回到团队插件分区', async () => {
    const user = userEvent.setup()
    renderPage('/plugin/templates?teamId=7')
    await screen.findByText('dynamic_greet')

    await user.click(screen.getByRole('button', { name: /返回插件/ }))
    expect(await screen.findByText('团队插件页面标记')).toBeInTheDocument()
  })

  it('不可管理成员访问团队模板页重定向回团队插件', async () => {
    vi.mocked(getTeamPlugins).mockResolvedValue({
      teamId: '7',
      myRole: 2,
      canManage: false,
      items: [],
    } as never)
    renderPage('/plugin/templates?teamId=7')

    expect(await screen.findByText('团队插件页面标记')).toBeInTheDocument()
  })
})

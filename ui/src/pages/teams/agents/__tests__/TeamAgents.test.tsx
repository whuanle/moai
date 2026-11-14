import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router'
import { ConfigProvider } from 'antd'
import zhCN from 'antd/locale/zh_CN'
import { TeamAgents } from '../TeamAgents'
import { deleteTeamAgent, getTeamAgent, getTeamAgents, setTeamAgentEnabled } from '@/api/team-agent'

vi.mock('@/api/team-agent', () => ({
  getTeamAgents: vi.fn().mockResolvedValue({ myRole: 2, items: [] }),
  getTeamAgent: vi.fn().mockResolvedValue({}),
  deleteTeamAgent: vi.fn().mockResolvedValue(undefined),
  createTeamAgent: vi.fn().mockResolvedValue('agent-id'),
  updateTeamAgent: vi.fn().mockResolvedValue(undefined),
  setTeamAgentAvatar: vi.fn().mockResolvedValue(undefined),
  setTeamAgentEnabled: vi.fn().mockResolvedValue(undefined),
  seedBuiltinTeamAgents: vi.fn().mockResolvedValue(0),
}))

vi.mock('@/api/aichannel', () => ({
  getTeamAvailableModels: vi.fn().mockResolvedValue([]),
}))

vi.mock('@/api/team-plugin', () => ({
  getTeamPlugins: vi.fn().mockResolvedValue({ teamId: 9, myRole: 2, canManage: true, items: [] }),
}))

vi.mock('@/api/wiki', () => ({
  getWikis: vi.fn().mockResolvedValue({ teamId: 9, myRole: 2, items: [] }),
}))

vi.mock('@/api/kiota', () => ({
  getApiClient: vi.fn(() => ({})),
}))

function renderTeamAgents(canManage = true) {
  return render(
    <ConfigProvider locale={zhCN}>
      <MemoryRouter initialEntries={['/team/9/agents']}>
        <Routes>
          <Route path="/team/:teamId" element={<TeamAgents teamId={9} canManage={canManage} />} />
          <Route path="/team/:teamId/agents" element={<TeamAgents teamId={9} canManage={canManage} />} />
          <Route path="/team/:teamId/agent/new" element={<div data-testid="editor-page">editor-page</div>} />
          <Route path="/team/:teamId/agent/:agentId/edit" element={<div data-testid="editor-page">editor-page</div>} />
        </Routes>
      </MemoryRouter>
    </ConfigProvider>,
  )
}

describe('TeamAgents', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(getTeamAgents).mockResolvedValue({
      myRole: 2,
      items: [
        {
          id: 'a0',
          name: '规划师 Plan',
          description: '内置规划智能体',
          avatarObjectKey: '',
          enabled: true,
          isSystem: true,
          subCount: 3,
          createUserName: 'owner',
          createTime: '2026-11-01T00:00:00Z',
          updateUserName: 'owner',
          updateTime: '2026-11-02T00:00:00Z',
        },
        {
          id: 'a1',
          name: '研究助手',
          description: '拆解任务并派发子智能体',
          avatarObjectKey: '',
          enabled: true,
          isSystem: false,
          subCount: 2,
          createUserName: 'owner',
          createTime: '2026-11-01T00:00:00Z',
          updateUserName: 'owner',
          updateTime: '2026-11-02T00:00:00Z',
        },
        {
          id: 'a2',
          name: '写作助手',
          description: '',
          avatarObjectKey: '',
          enabled: false,
          isSystem: false,
          subCount: 0,
          createUserName: 'owner',
          createTime: '2026-11-01T00:00:00Z',
          updateUserName: 'owner',
          updateTime: '2026-11-02T00:00:00Z',
        },
      ],
    })
  })

  it('卡片列表展示名称/启停开关/内置标记/子智能体数，不使用表格', async () => {
    renderTeamAgents()

    expect(await screen.findByText('研究助手')).toBeInTheDocument()
    expect(screen.getByText('写作助手')).toBeInTheDocument()
    // 管理员：每张卡一个启停开关（三张卡）
    expect(screen.getAllByRole('switch')).toHaveLength(3)
    // 内置智能体带「内置」标记
    expect(screen.getByText('内置')).toBeInTheDocument()
    // 子智能体数徽标
    expect(screen.getByText('2 个子智能体')).toBeInTheDocument()
    // 卡片展示描述；空描述回显占位符
    expect(screen.getByText('内置规划智能体')).toBeInTheDocument()
    expect(screen.getByText('拆解任务并派发子智能体')).toBeInTheDocument()
    expect(screen.getByText('-')).toBeInTheDocument()
    // 卡片视图：无表格、无条数统计文本
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
    expect(screen.queryByText('共 3 条')).not.toBeInTheDocument()
    // 列表接口带团队 id 调用
    expect(getTeamAgents).toHaveBeenCalledWith(9, { searchText: undefined })
  })

  it('成员视图启停展示为 Tag，不提供开关', async () => {
    renderTeamAgents(false)

    await screen.findByText('研究助手')
    expect(screen.queryByRole('switch')).not.toBeInTheDocument()
    // 两个启用（规划师/研究助手）+ 一个停用（写作助手）
    expect(screen.getAllByText('启用').length).toBeGreaterThanOrEqual(2)
    expect(screen.getByText('停用')).toBeInTheDocument()
  })

  it('卡片启停开关直切调用启停接口', async () => {
    renderTeamAgents(true)

    await screen.findByText('研究助手')
    // 第一张卡是「规划师 Plan」(a0)，当前启用，切到停用
    fireEvent.click(screen.getAllByRole('switch')[0])
    await waitFor(() => {
      expect(setTeamAgentEnabled).toHaveBeenCalledWith('a0', false)
    })
  })

  it('canManage=false 时隐藏新建/编辑/删除入口', async () => {
    renderTeamAgents(false)

    await screen.findByText('研究助手')
    expect(screen.queryByText('新建智能体')).not.toBeInTheDocument()
    expect(screen.queryByText('编辑')).not.toBeInTheDocument()
    expect(screen.queryByText('删除')).not.toBeInTheDocument()
  })

  it('内置智能体不提供删除入口，普通智能体提供', async () => {
    renderTeamAgents(true)

    await screen.findByText('研究助手')
    // 只有两个普通智能体（研究助手/写作助手）各一个删除按钮（纯图标 + aria-label），内置卡没有
    expect(screen.getAllByRole('button', { name: '删除' })).toHaveLength(2)
  })

  it('canManage=true 时展示新建入口并跳转编辑页', async () => {
    renderTeamAgents(true)

    await screen.findByText('研究助手')
    fireEvent.click(screen.getByText('新建智能体'))
    await waitFor(() => {
      expect(screen.getByTestId('editor-page')).toBeInTheDocument()
    })
  })

  it('关键字搜索触发带 searchText 的查询', async () => {
    renderTeamAgents()

    await screen.findByText('研究助手')
    fireEvent.change(screen.getByPlaceholderText('按名称/描述搜索'), { target: { value: '研究' } })
    fireEvent.click(screen.getByText('查询'))
    await waitFor(() => {
      expect(getTeamAgents).toHaveBeenLastCalledWith(9, { searchText: '研究' })
    })
  })

  it('删除经 Popconfirm 确认后调用删除并刷新列表', async () => {
    renderTeamAgents(true)

    await screen.findByText('写作助手')
    // 卡片底栏删除按钮（纯图标 + aria-label）；内置卡无删除，第一个是研究助手
    fireEvent.click(screen.getAllByRole('button', { name: '删除' })[0])
    fireEvent.click(await screen.findByRole('button', { name: /确\s*定/ }))
    await waitFor(() => {
      expect(deleteTeamAgent).toHaveBeenCalledWith('a1')
    })
    await waitFor(() => {
      expect(getTeamAgents).toHaveBeenCalledTimes(2)
    })
  })

  it('详情按钮拉取详情并打开弹窗', async () => {
    vi.mocked(getTeamAgent).mockResolvedValue({
      id: 'a1',
      key: 'research_helper',
      name: '研究助手',
      description: '拆解任务',
      enabled: true,
      mainPrompt: '你是团队负责人',
      mainPluginIds: [],
      mainWikiIds: [],
      mainGraphIds: [],
      subs: [{ key: 'researcher', name: '资料调研员', description: '负责检索', prompt: '你是检索专员', pluginIds: [], wikiIds: [], graphIds: [] }],
    })
    renderTeamAgents(true)

    await screen.findByText('研究助手')
    // 内置智能体排序在最前，第一张卡是「规划师 Plan」(a0)
    fireEvent.click(screen.getAllByRole('button', { name: '详情' })[0])
    await waitFor(() => {
      expect(getTeamAgent).toHaveBeenCalledWith('a0')
    })
    expect(await screen.findByText('researcher')).toBeInTheDocument()
  })
})

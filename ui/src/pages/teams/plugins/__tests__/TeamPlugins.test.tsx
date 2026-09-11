import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { TeamPlugins } from '../TeamPlugins'
import { getTeamPlugins } from '@/api/team-plugin'
import { useAppStore } from '@/store/app'

vi.mock('@/api/team-plugin', () => ({
  getTeamPlugins: vi.fn(),
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
  })

  it('分为自定义与动态两个 tab', async () => {
    render(<TeamPlugins teamId={7} />)

    await waitFor(() => {
      expect(getTeamPlugins).toHaveBeenCalledWith(7)
    })
    expect(await screen.findByRole('tab', { name: '自定义插件' })).toBeInTheDocument()
    expect(screen.getByRole('tab', { name: '动态插件' })).toBeInTheDocument()
  })

  it('自定义 tab 展示自定义插件，动态 tab 展示动态实例', async () => {
    const user = userEvent.setup()
    render(<TeamPlugins teamId={7} />)

    expect(await screen.findByText('weather')).toBeInTheDocument()
    expect(screen.queryByText('greet')).not.toBeInTheDocument()

    await user.click(screen.getByRole('tab', { name: '动态插件' }))
    expect(await screen.findByText('greet')).toBeInTheDocument()
  })

  it('可管理时展示导入/新建入口', async () => {
    render(<TeamPlugins teamId={7} />)
    await screen.findByText('weather')

    expect(screen.getByRole('button', { name: /导入 MCP/ })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /导入 OpenAPI/ })).toBeInTheDocument()
  })
})

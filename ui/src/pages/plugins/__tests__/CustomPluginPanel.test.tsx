import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { CustomPluginPanel } from '../CustomPluginPanel'
import { customPluginApi } from '@/api/plugin'

vi.mock('@/api/plugin', () => ({
  customPluginApi: {
    getCustomPlugins: vi.fn(),
    getCustomPluginDetail: vi.fn(),
    getCustomPluginFunctions: vi.fn(),
    importMcp: vi.fn(),
    updateMcp: vi.fn(),
    importOpenApi: vi.fn(),
    updateOpenApi: vi.fn(),
    refreshMcp: vi.fn(),
    deleteCustomPlugin: vi.fn(),
    preUploadOpenApiFile: vi.fn(),
  },
}))

const MOCK_ITEMS = [
  {
    pluginId: 'p-1',
    pluginName: 'weather',
    title: '天气',
    type: 'mcp' as const,
    server: 'http://mcp.example.com',
    classifyId: 1,
    description: '查询天气',
    isPublic: true,
    counter: '3',
    createUserName: 'admin',
    createTime: '2026-09-01T10:00:00',
  },
  {
    pluginId: 'p-2',
    pluginName: 'translate',
    title: '翻译',
    type: 'openApi' as const,
    server: 'http://api.example.com',
    classifyId: 0,
    description: '翻译文本',
    isPublic: false,
    counter: '0',
    createTime: '2026-09-02T11:30:00',
  },
]

describe('CustomPluginPanel', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(customPluginApi.getCustomPlugins).mockResolvedValue(MOCK_ITEMS)
  })

  it('渲染自定义插件列表', async () => {
    render(<CustomPluginPanel classifies={[{ classifyId: 1, name: '工具' }]} />)

    await waitFor(() => {
      expect(customPluginApi.getCustomPlugins).toHaveBeenCalled()
    })
    expect(await screen.findByText('weather')).toBeInTheDocument()
    expect(screen.getByText('translate')).toBeInTheDocument()
  })

  it('MCP 插件卡片渲染查看/编辑/删除操作，刷新函数收进更多菜单', async () => {
    const user = userEvent.setup()
    render(<CustomPluginPanel classifies={[]} />)
    await screen.findByText('weather')

    // 切到列表视图，检查行内操作按钮
    await user.click(screen.getByText('列表'))
    const row = Array.from(document.querySelectorAll('table tr')).find((r) =>
      r.textContent?.includes('weather'),
    )
    const labels = Array.from(row?.querySelectorAll('button') ?? [])
      .map((b) => b.getAttribute('aria-label'))
      .filter(Boolean)
    expect(labels).toContain('查看函数')
    expect(labels).toContain('编辑插件')
    expect(labels).toContain('删除插件')
    expect(labels).not.toContain('刷新函数')

    // 更多菜单里包含刷新函数（MCP 专属）
    const moreBtn = row?.querySelector('button[aria-label="更多操作"]')
    expect(moreBtn).toBeTruthy()
    await user.click(moreBtn!)
    expect(await screen.findByText('刷新函数')).toBeInTheDocument()
  })

  it('点击删除触发确认并在确认后调用删除接口', async () => {
    const user = userEvent.setup()
    vi.mocked(customPluginApi.deleteCustomPlugin).mockResolvedValue(undefined)
    render(<CustomPluginPanel classifies={[]} />)
    await screen.findByText('weather')

    // 卡片视图下第一张卡即 weather（MOCK 首项），删除按钮内联在卡片操作区
    const delBtn = screen.getAllByRole('button', { name: '删除插件' })[0]!
    await user.click(delBtn)
    // Popconfirm 弹出确认按钮（无 ConfigProvider 环境下默认英文 OK）
    const okBtn = await screen.findByRole('button', { name: /ok/i })
    await user.click(okBtn)
    await waitFor(() => {
      expect(customPluginApi.deleteCustomPlugin).toHaveBeenCalledWith('p-1')
    })
  })
})

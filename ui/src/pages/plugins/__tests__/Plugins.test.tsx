import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router'
import { Plugins } from '../Plugins'
import { useAppStore } from '@/store/app'
import { pluginApi } from '@/api/plugin'

vi.mock('@/api/plugin', () => ({
  pluginApi: {
    getManagePlugins: vi.fn(),
  },
  customPluginApi: {
    getCustomPlugins: vi.fn().mockResolvedValue([]),
    getCustomPluginDetail: vi.fn(),
    getCustomPluginFunctions: vi.fn().mockResolvedValue({ items: [] }),
    importMcp: vi.fn(),
    updateMcp: vi.fn(),
    importOpenApi: vi.fn(),
    updateOpenApi: vi.fn(),
    refreshMcp: vi.fn(),
    deleteCustomPlugin: vi.fn(),
    preUploadOpenApiFile: vi.fn(),
  },
}))

vi.mock('@/api/classify', () => ({
  classifyApi: {
    getClassifies: vi.fn().mockResolvedValue([]),
    getPluginClassifies: vi.fn().mockResolvedValue([]),
  },
  classifyLabel: (c: { emoji?: string | null; name?: string | null }) => [c?.emoji, c?.name].filter(Boolean).join(' '),
  ClassifyType: { Plugin: 'plugin', App: 'app', Kb: 'kb', Prompt: 'prompt', Skill: 'skill' },
}))

/** 内存注册表合并行：无 DB 记录的静态插件 id 全部为 Guid.Empty（重复记录 bug 的触发条件） */
const EMPTY_GUID = '00000000-0000-0000-0000-000000000000'

function mockMemoryStatics(keys: string[]) {
  vi.mocked(pluginApi.getManagePlugins).mockResolvedValue(
    keys.map((key, index) => ({
      id: EMPTY_GUID,
      pluginName: key,
      title: `插件${index + 1}`,
      type: 2,
      classifyId: 0,
      classifyName: null,
      kind: 'static',
      isSystem: true,
      isPublic: true,
      avatarPath: '',
      createTime: '2026-09-23T00:00:00Z',
      createUserId: 0,
      updateUserId: 0,
      pluginKey: key,
      paramsExample: null,
    })) as never,
  )
}

function renderPluginsTab() {
  return render(
    <MemoryRouter initialEntries={['/plugins?tab=static']}>
      <Routes>
        <Route path="/plugins" element={<Plugins />} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('Plugins 静态插件页签（重复记录回归）', () => {
  let consoleSpy: ReturnType<typeof vi.spyOn>

  beforeEach(() => {
    vi.clearAllMocks()
    useAppStore.setState({
      userInfo: { accessToken: 'token', userId: '1', userName: 'admin', isAdmin: true },
    })
    consoleSpy = vi.spyOn(console, 'error').mockImplementation(() => undefined)
  })

  afterEach(() => {
    consoleSpy.mockRestore()
  })

  it('多个内存静态插件（id 全为 Guid.Empty）行 key 唯一：无重复 key 警告，刷新后行数不重复', async () => {
    mockMemoryStatics(['web_search', 'time_now', 'calc_tool'])
    renderPluginsTab()

    expect(await screen.findByText('web_search')).toBeInTheDocument()
    expect(screen.getByText('time_now')).toBeInTheDocument()
    expect(screen.getByText('calc_tool')).toBeInTheDocument()

    // 触发一次列表状态更新（点刷新按钮），模拟「编辑保存后 reload」的渲染路径
    const user = userEvent.setup()
    await user.click(await screen.findByRole('button', { name: /刷新/ }))
    await waitFor(() => {
      expect(pluginApi.getManagePlugins).toHaveBeenCalledTimes(2)
    })

    // 旧行为：重复 React key 会在 console.error 打 "Encountered two children with the same key"
    const keyWarnings = consoleSpy.mock.calls.filter((c: unknown[]) =>
      String(c[0]).includes('two children with the same key'),
    )
    expect(keyWarnings).toHaveLength(0)

    // 每行内容恰好出现一次（无重复渲染）
    for (const key of ['web_search', 'time_now', 'calc_tool']) {
      expect(screen.getAllByText(key)).toHaveLength(1)
    }
  })
})

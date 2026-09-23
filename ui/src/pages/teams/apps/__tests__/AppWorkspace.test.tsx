import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router'
import { AppWorkspace } from '../AppWorkspace'
import { getAppAgentConfig, getAppDetail, publishApp } from '@/api/app'

vi.mock('@/api/classify', () => ({
  classifyApi: { getClassifies: vi.fn().mockResolvedValue([]) },
  classifyLabel: (c: { emoji?: string | null; name?: string | null }) => [c?.emoji, c?.name].filter(Boolean).join(' '),
  ClassifyType: { Plugin: 'plugin', App: 'app', Kb: 'kb', Prompt: 'prompt', Skill: 'skill' },
}))
vi.mock('@/api/app', () => ({
  getAppDetail: vi.fn(),
  getAppAgentConfig: vi.fn().mockResolvedValue({ appId: 'a1', appType: 'agent', prompt: '', modelId: null, wikiIds: [], plugins: [] }),
  getSandboxLimits: vi.fn().mockResolvedValue({ maxTtlSeconds: 86400, maxCpu: '4', maxMemory: '8Gi' }),
  saveAppAgentConfig: vi.fn(),
  updateApp: vi.fn(),
  uploadAppAvatar: vi.fn(),
  publishApp: vi.fn(),
  unpublishApp: vi.fn(),
  createDebugSession: vi.fn().mockResolvedValue('0198f2c1-1111-7000-8000-000000000001'),
}))
vi.mock('@/api/gateway', () => ({ getTeamGatewayModels: vi.fn().mockResolvedValue([]) }))
vi.mock('@/api/team-plugin', () => ({ getTeamPlugins: vi.fn().mockResolvedValue({ items: [] }) }))
vi.mock('@/api/skills', () => ({ getSkillOptions: vi.fn().mockResolvedValue([]) }))
vi.mock('@/api/wiki', () => ({ getWikis: vi.fn().mockResolvedValue({ items: [] }) }))
vi.mock('@/api/knowledgeGraph', () => ({ getKnowledgeGraphs: vi.fn().mockResolvedValue({ items: [] }) }))
vi.mock('@/api/agentChat', () => ({
  createAppChatAgent: vi.fn(() => ({})),
  runAppChat: vi.fn().mockResolvedValue(undefined),
  abortAppChat: vi.fn(),
}))

function renderPage(path = '/team/3/app/a1') {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route path="/team/:teamId/app/:appId/:section?" element={<AppWorkspace />} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('AppWorkspace（应用工作台）', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(getAppDetail).mockResolvedValue({
      appId: 'a1',
      teamId: '3',
      name: '客服助手',
      appType: 'agent',
      avatarPath: '',
      isExternal: false,
      publishStatus: 0,
      myRole: 2,
    } as never)
  })

  it('内部应用展示配置/信息/日志/监控/外部渠道菜单项，无访问点', async () => {
    renderPage()
    expect(await screen.findByRole('menuitem', { name: /配置/ })).toBeTruthy()
    expect(screen.getByRole('menuitem', { name: /信息/ })).toBeTruthy()
    expect(screen.getByRole('menuitem', { name: /日志/ })).toBeTruthy()
    expect(screen.getByRole('menuitem', { name: /监控/ })).toBeTruthy()
    expect(screen.getByRole('menuitem', { name: /外部渠道/ })).toBeTruthy()
    expect(screen.queryByRole('menuitem', { name: /访问点/ })).toBeNull()
  })

  it('外部应用额外展示访问点菜单项，不展示外部渠道', async () => {
    vi.mocked(getAppDetail).mockResolvedValue({
      appId: 'a2',
      teamId: '3',
      name: '外部客服',
      appType: 'agent',
      avatarPath: '',
      isExternal: true,
      publishStatus: 0,
      myRole: 2,
    } as never)

    renderPage('/team/3/app/a2')

    expect(await screen.findByRole('menuitem', { name: /访问点/ })).toBeTruthy()
    expect(screen.queryByRole('menuitem', { name: /外部渠道/ })).toBeNull()
  })

  it('Member 只看到配置与信息菜单项，无调试对话', async () => {
    vi.mocked(getAppDetail).mockResolvedValue({
      appId: 'a1',
      teamId: '3',
      name: '客服助手',
      appType: 'agent',
      avatarPath: '',
      isExternal: false,
      publishStatus: 1,
      myRole: 0,
    } as never)

    renderPage()

    expect(await screen.findByRole('menuitem', { name: /配置/ })).toBeTruthy()
    expect(screen.getByRole('menuitem', { name: /信息/ })).toBeTruthy()
    await waitFor(() => expect(screen.queryByRole('menuitem', { name: /日志/ })).toBeNull())
    expect(screen.queryByRole('menuitem', { name: /外部渠道/ })).toBeNull()
    expect(screen.getByText(/调试需要团队管理员权限/)).toBeTruthy()
  })

  it('已发布且有未发布配置变更：头部与配置区提供「重新发布」，确认后草稿上线并清除警告', async () => {
    vi.mocked(getAppDetail).mockResolvedValue({
      appId: 'a1',
      teamId: '3',
      name: '客服助手',
      appType: 'agent',
      avatarPath: '',
      isExternal: false,
      publishStatus: 1,
      myRole: 2,
    } as never)
    // 配置状态 0=草稿有未发布变更（配置分区加载后上报工作台）
    vi.mocked(getAppAgentConfig).mockResolvedValue({
      appId: 'a1',
      appType: 'agent',
      prompt: '',
      modelId: null,
      wikiIds: [],
      plugins: [],
      status: 0,
    } as never)
    vi.mocked(publishApp).mockResolvedValue(undefined)

    renderPage()

    // 详情与配置加载完成后：头部主按钮 + 配置区警告条 action 各一个
    await screen.findByRole('button', { name: '取消发布' })
    await waitFor(() => expect(screen.getAllByRole('button', { name: '重新发布' }).length).toBe(2))
    const republishBtns = screen.getAllByRole('button', { name: '重新发布' })
    expect(screen.getByText(/已有未发布的配置修改/)).toBeTruthy()
    expect(screen.getByRole('button', { name: '取消发布' })).toBeTruthy()

    // 头部按钮（先渲染）确认重新发布（antd 对两字中文按钮自动插空格）
    fireEvent.click(republishBtns[0])
    fireEvent.click(await screen.findByRole('button', { name: /确\s*定/ }))

    await waitFor(() => expect(publishApp).toHaveBeenCalledWith('a1'))
    // 重新发布后：入口与警告条消失，恢复只有「取消发布」
    //（antd loading 图标退场动画在 jsdom 不结束，可访问名短暂带 loading 前缀，故用正则）
    await waitFor(() => expect(screen.queryByRole('button', { name: '重新发布' })).toBeNull())
    expect(screen.queryByText(/已有未发布的配置修改/)).toBeNull()
    await waitFor(() => expect(screen.getByRole('button', { name: /取消发布/ })).toBeTruthy())
  })
})

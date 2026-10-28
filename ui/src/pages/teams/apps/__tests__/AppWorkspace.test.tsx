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
  getCompactionDefaults: vi.fn().mockResolvedValue({
    toolResultTriggerMessages: 16,
    summarizeTriggerTokens: 16000,
    preserveTurns: 8,
    hardTokenLimit: 64000,
    enableSummarization: false,
    summarizerModelId: '',
  }),
  saveAppAgentConfig: vi.fn(),
  getAppSecurity: vi.fn().mockResolvedValue({ appId: 'a1', enabled: false, maskToolResult: true, maskToolArgs: false, maskModelOutput: false, maskInput: false, rules: [], modelOutputRules: [], myRole: 2 }),
  getAppSecurityEvents: vi.fn().mockResolvedValue({ items: [], total: 0, pageNo: 1, pageSize: 10 }),
  getAppUsage: vi.fn().mockResolvedValue({
    summary: { callCount: 0, promptTokens: 0, completionTokens: 0, totalTokens: 0 },
    byModel: [],
    stats: { sessionCount: 0, userCount: 0, messageCount: 0, lastActiveTime: null },
  }),
  saveAppSecurity: vi.fn(),
  getMemoryDefaults: vi.fn().mockResolvedValue({ prompt: '平台默认记忆提示词' }),
  SECURITY_RULE_TYPES: ['phone', 'idCard', 'email', 'bankCard', 'custom'],
  updateApp: vi.fn(),
  uploadAppAvatar: vi.fn(),
  publishApp: vi.fn(),
  unpublishApp: vi.fn(),
  createDebugSession: vi.fn().mockResolvedValue('0198f2c1-1111-7000-8000-000000000001'),
}))
vi.mock('@/api/gateway', () => ({ getTeamGatewayModels: vi.fn().mockResolvedValue([]) }))
vi.mock('@/api/team-plugin', () => ({ getTeamPlugins: vi.fn().mockResolvedValue({ items: [] }) }))
vi.mock('@/api/skills', () => ({ getSkillOptions: vi.fn().mockResolvedValue([]) }))
vi.mock('@/api/wiki', () => ({
  getWikis: vi.fn().mockResolvedValue({ items: [] }),
  getWikiModelOptions: vi.fn().mockResolvedValue({ embeddingModels: [] }),
}))
vi.mock('@/api/knowledgeGraph', () => ({ getKnowledgeGraphs: vi.fn().mockResolvedValue({ items: [] }) }))
vi.mock('@/api/agentChat', () => ({
  createAppChatAgent: vi.fn(() => ({})),
  runAppChat: vi.fn().mockResolvedValue(undefined),
  abortAppChat: vi.fn(),
}))
vi.mock('@/api/memory', () => ({
  clearAppMemory: vi.fn().mockResolvedValue(undefined),
  getAppMemoryStats: vi.fn().mockResolvedValue({ totalMemories: 0 }),
  getMyMemories: vi.fn().mockResolvedValue({ items: [], total: 0, memoryEnabled: true }),
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

  it('内部应用展示配置/信息/日志/外部渠道菜单项：内容脱敏与长期记忆并入配置收缩卡片、监控并入日志 Tab，无独立安全/记忆/监控菜单与访问点', async () => {
    renderPage()
    expect(await screen.findByRole('menuitem', { name: /配置/ })).toBeTruthy()
    expect(screen.getByRole('menuitem', { name: /信息/ })).toBeTruthy()
    // 安全（内容脱敏）与长期记忆不再单独成菜单，由「配置」分区收缩卡片承载
    expect(screen.queryByRole('menuitem', { name: /安全/ })).toBeNull()
    expect(screen.queryByRole('menuitem', { name: /长期记忆/ })).toBeNull()
    expect(await screen.findByText('内容脱敏')).toBeTruthy()
    expect(screen.getByText('长期记忆')).toBeTruthy()
    expect(screen.getByRole('menuitem', { name: /日志/ })).toBeTruthy()
    // 监控（使用分析）并入「日志」Tab 容器，不再单独成菜单
    expect(screen.queryByRole('menuitem', { name: /监控/ })).toBeNull()
    expect(screen.getByRole('menuitem', { name: /外部渠道/ })).toBeTruthy()
    expect(screen.queryByRole('menuitem', { name: /访问点/ })).toBeNull()
  })

  it('外部应用额外展示访问点与发布菜单项，不展示外部渠道与上架管理', async () => {
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
    // 外部应用无上架审批：菜单为「发布」（非「上架管理」），直接发布带版本号
    expect(screen.getByRole('menuitem', { name: /发布/ })).toBeTruthy()
    expect(screen.queryByRole('menuitem', { name: /上架管理/ })).toBeNull()
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

  it('已发布且有未发布配置变更：仅配置区警告条提供「重新发布」（头部不提供发布入口），一键重发后警告消失', async () => {
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

    // 详情与配置加载完成后：仅配置区警告条 action 一个「重新发布」；头部不再提供发布/重新发布（收敛到「上架管理」分区）
    await waitFor(() => expect(screen.getAllByRole('button', { name: '重新发布' }).length).toBe(1))
    expect(screen.getByText(/已有未发布的配置修改/)).toBeTruthy()
    expect(screen.queryByRole('button', { name: /^发\s*布$/ })).toBeNull()
    expect(screen.queryByRole('button', { name: /撤回发布/ })).toBeNull()

    // 配置区警告条一键重发：不携带版本号（版本保持原值），警告条消失
    fireEvent.click(screen.getByRole('button', { name: '重新发布' }))

    await waitFor(() => expect(publishApp).toHaveBeenCalledWith('a1'))
    await waitFor(() => expect(screen.queryByText(/已有未发布的配置修改/)).toBeNull())
  })

  it('未发布：头部不提供发布/重新发布入口（发布收敛在「上架管理」分区），已发布后露出「进入对话」', async () => {
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

    renderPage()

    // 头部只有发布状态 Tag、「进入对话」（未发布时不出现）与「返回列表」
    expect(await screen.findByText('未发布')).toBeTruthy()
    expect(screen.queryByRole('button', { name: /^发\s*布$/ })).toBeNull()
    expect(screen.queryByRole('button', { name: /重新发布/ })).toBeNull()
    expect(screen.queryByRole('button', { name: '进入对话' })).toBeNull()
    expect(screen.getByRole('button', { name: '返回应用列表' })).toBeTruthy()
  })
})

import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { AppDebugChat } from '../chat/AppDebugChat'
import { createDebugSession, decideAppSessionToolApproval, getAppUserConfig, saveAppUserConfig } from '@/api/app'
import { getTeamAvailableModels } from '@/api/aichannel'
import { createAppChatAgent, runAppChat } from '@/api/agentChat'

/** userconfig 默认桩（审批 auto、无豁免清单、无可选模型） */
const DEFAULT_USER_CONFIG = {
  promptId: 0,
  defaultModelId: '',
  modelIds: [],
  teamAgents: [],
  defaultTeamAgentId: '',
  skills: [],
  defaultSkills: [],
  skillOptions: [],
  promptOptions: [],
  toolApprovalMode: 'auto' as const,
  toolApprovalExemptNames: [],
  toolApprovalExemptPrefixes: [],
  toolApprovalAutoApprovedNames: [],
  toolApprovalAutoApprovedPrefixes: [],
  memoryMode: 0,
  memoryEnabled: false,
  memoryGlobalMode: 0,
  memoryGlobalAllowed: false,
}

vi.mock('@/api/app', () => ({
  createDebugSession: vi.fn().mockResolvedValue('0198f2c1-1111-7000-8000-000000000001'),
  // 调试会话审批卡走同端点决策（@AP-S149）；审批模式不持久化，saveAppUserConfig 断言未被调用
  decideAppSessionToolApproval: vi.fn(),
  saveAppUserConfig: vi.fn(),
  // 调试团队智能体切换器经 agentConfig 取草稿绑定（rounds 325~331 起）；默认空绑定不出切换器
  getAppAgentConfig: vi.fn().mockResolvedValue({
    appId: 'a1',
    prompt: '',
    modelId: null,
    modelIds: [],
    wikiIds: [],
    graphIds: [],
    plugins: [],
    skills: [],
    teamAgentIds: [],
    defaultTeamAgentId: null,
    teamAgents: [],
  }),
  // 调试会话切换器经 userconfig 取草稿可选模型与审批配置（rounds 299~324 起）
  getAppUserConfig: vi.fn().mockResolvedValue({
    promptId: 0,
    defaultModelId: '',
    modelIds: [],
    teamAgents: [],
    defaultTeamAgentId: '',
    skills: [],
    defaultSkills: [],
    skillOptions: [],
    promptOptions: [],
    toolApprovalMode: 'auto',
    toolApprovalExemptNames: [],
    toolApprovalExemptPrefixes: [],
    toolApprovalAutoApprovedNames: [],
    toolApprovalAutoApprovedPrefixes: [],
    memoryMode: 0,
    memoryEnabled: false,
    memoryGlobalMode: 0,
    memoryGlobalAllowed: false,
  }),
}))

vi.mock('@/api/aichannel', () => ({
  getTeamAvailableModels: vi.fn().mockResolvedValue([]),
}))

vi.mock('@/api/agentChat', () => ({
  createAppChatAgent: vi.fn(() => ({})),
  runAppChat: vi.fn().mockResolvedValue(undefined),
  abortAppChat: vi.fn(),
}))

describe('AppDebugChat（调试对话面板）', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(createDebugSession).mockResolvedValue('0198f2c1-1111-7000-8000-000000000001')
    vi.mocked(getAppUserConfig).mockResolvedValue(DEFAULT_USER_CONFIG)
  })

  it('进入即创建调试会话并提示对话不保存', async () => {
    render(
      <MemoryRouter>
        <AppDebugChat appId="a1" />
      </MemoryRouter>,
    )

    expect(screen.getByText(/对话不保存/)).toBeTruthy()
    await waitFor(() => expect(createDebugSession).toHaveBeenCalledWith('a1'))
  })

  it('欢迎态复刻真实对话界面：徽标+应用名+开场白简报卡+快捷输入，占位为「发消息给 {应用名}」（@AP-S147）', async () => {
    render(
      <MemoryRouter>
        <AppDebugChat
          appId="a1"
          appName="数据分析助手"
          appAvatar=""
          openingStatement={'你好，我是分析助手 📊\n\n- **趋势分析**：折线图\n- **品类诊断**：柱状图'}
          quickInputs={['分析近30天销售趋势', '  ', '找销售额Top10']}
        />
      </MemoryRouter>,
    )

    // 欢迎态：应用名标题 + 占位「发消息给 …」
    await waitFor(() => expect(screen.getByText('数据分析助手')).toBeInTheDocument())
    expect(screen.getByPlaceholderText(/发消息给 数据分析助手/)).toBeInTheDocument()
    // 开场白以 Markdown 简报卡渲染（**加粗** 渲染为 strong）
    await waitFor(() => expect(screen.getAllByText('趋势分析').length).toBeGreaterThan(0))
    expect(document.querySelectorAll('.moai-chat__landing-briefing-body strong').length).toBeGreaterThan(0)
    // 快捷输入过滤空白项并渲染为建议卡片
    expect(screen.getByText('分析近30天销售趋势')).toBeInTheDocument()
    expect(screen.getByText('找销售额Top10')).toBeInTheDocument()
  })

  it('开场白不混入消息流：发送首条消息后切换为消息流 + 停靠输入卡（@AP-S147）', async () => {
    const { runAppChat } = await import('@/api/agentChat')
    vi.mocked(runAppChat).mockImplementation(async (_agent, text, handlers) => {
      handlers?.onDelta?.('你好！我是小析。')
    })

    render(
      <MemoryRouter>
        <AppDebugChat
          appId="a1"
          appName="数据分析助手"
          openingStatement="我是开场白，只出现在欢迎态"
        />
      </MemoryRouter>,
    )
    await waitFor(() => expect(screen.getByText('我是开场白，只出现在欢迎态')).toBeInTheDocument())

    fireEvent.change(screen.getByPlaceholderText(/发消息给 数据分析助手/), { target: { value: '你好' } })
    fireEvent.click(screen.getByRole('button', { name: '发送' }))

    // 进入对话态：开场白简报卡消失，用户消息与回复出现在消息流
    await waitFor(() => expect(screen.queryByText('我是开场白，只出现在欢迎态')).not.toBeInTheDocument())
    expect(screen.getByText('你好')).toBeInTheDocument()
    await waitFor(() => expect(screen.getByText('你好！我是小析。')).toBeInTheDocument())
  })

  it('输入卡复用真实对话页全量组件：附件/审批模式切换/模型切换器，切换仅本地生效（@AP-S148）', async () => {
    vi.mocked(getAppUserConfig).mockResolvedValue({
      ...DEFAULT_USER_CONFIG,
      defaultModelId: 'm1',
      modelIds: ['m1', 'm2'],
    })
    vi.mocked(getTeamAvailableModels).mockResolvedValue([
      { aiModelId: 'm1', name: 'GLM-5.3-Flash', modelId: 'glm-flash', channelName: '渠道A', modelKind: 'conversation' },
      { aiModelId: 'm2', name: 'GLM-4', modelId: 'glm4', channelName: '渠道B', modelKind: 'conversation' },
    ] as never)

    render(
      <MemoryRouter>
        <AppDebugChat appId="a1" appName="数据分析助手" />
      </MemoryRouter>,
    )
    await waitFor(() => expect(screen.getByPlaceholderText(/发消息给 数据分析助手/)).toBeInTheDocument())

    // 共享输入卡（ChatComposer）全量组件：附件按钮 + 审批模式切换（初始取用户级配置 auto）+ 模型切换器
    expect(screen.getByRole('button', { name: '上传附件' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /自动模式/ })).toBeInTheDocument()
    expect(document.querySelector('.moai-chat__composer .ant-select')).not.toBeNull()

    // 切换为审批模式：调试面板仅本地生效，不持久化用户级应用配置（与真实对话页差异）
    fireEvent.click(screen.getByRole('button', { name: /自动模式/ }))
    expect(screen.getByRole('button', { name: /审批模式/ })).toBeInTheDocument()
    expect(saveAppUserConfig).not.toHaveBeenCalled()
  })

  it('审批模式：call_tool 挂起审批卡，批准走调试会话决策接口（@AP-S149）', async () => {
    vi.mocked(getAppUserConfig).mockResolvedValue({
      ...DEFAULT_USER_CONFIG,
      toolApprovalMode: 'approval',
      toolApprovalExemptNames: ['search_knowledge_base'],
      toolApprovalExemptPrefixes: ['skill_'],
    })
    let finishRun: (() => void) | undefined
    vi.mocked(runAppChat).mockImplementation(async (_agent, _text, handlers) => {
      handlers?.onToolCall?.({ id: 'tc-1', name: 'call_tool' })
      handlers?.onToolCallEnd?.({
        id: 'tc-1',
        name: 'call_tool',
        args: { toolName: 'sandbox_run_code', argumentsJson: '{"language":"python","code":"print(1)"}' },
      })
      await new Promise<void>((resolve) => {
        finishRun = resolve
      })
    })

    render(
      <MemoryRouter>
        <AppDebugChat appId="a1" appName="数据分析助手" />
      </MemoryRouter>,
    )
    await waitFor(() => expect(screen.getByPlaceholderText(/发消息给 数据分析助手/)).toBeInTheDocument())

    fireEvent.change(screen.getByPlaceholderText(/发消息给 数据分析助手/), { target: { value: '执行一段代码' } })
    fireEvent.click(screen.getByRole('button', { name: '发送' }))

    // 审批卡：真实工具名 + 等待审批 + 批准/拒绝按钮；请求头携带审批模式
    await waitFor(() => {
      expect(screen.getByText('sandbox_run_code')).toBeInTheDocument()
    })
    expect(screen.getByText('等待审批')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: '批准执行' })).toBeInTheDocument()
    expect(vi.mocked(createAppChatAgent).mock.calls.at(-1)?.[2]?.toolApprovalMode).toBe('approval')

    vi.mocked(decideAppSessionToolApproval).mockResolvedValue('approved')
    fireEvent.click(screen.getByRole('button', { name: '批准执行' }))

    // 决策走调试会话 id；批准生效：卡片进入「已批准 · 执行中」
    await waitFor(() => {
      expect(decideAppSessionToolApproval).toHaveBeenCalledWith('0198f2c1-1111-7000-8000-000000000001', 'sandbox_run_code', true)
    })
    await waitFor(() => {
      expect(screen.getByText('已批准 · 执行中')).toBeInTheDocument()
    })

    finishRun?.()
  })
})

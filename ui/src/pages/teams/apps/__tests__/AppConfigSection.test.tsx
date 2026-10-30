import { describe, expect, it, vi, beforeEach } from 'vitest'
import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { AppConfigSection, type AppDetail } from '../AppConfigSection'
import { getAppAgentConfig, getAppSecurity, getApps, getSandboxLimits, publishApp, saveAppAgentConfig, saveAppSecurity } from '@/api/app'
import { getTeamAvailableModels } from '@/api/aichannel'
import { getTeamPlugins } from '@/api/team-plugin'
import { getSkillOptions } from '@/api/skills'
import { getWikis } from '@/api/wiki'
import { getKnowledgeGraphs } from '@/api/knowledgeGraph'

vi.mock('@/api/app', () => ({
  getAppDetail: vi.fn(),
  getAppAgentConfig: vi.fn(),
  getApps: vi.fn(),
  getCompactionDefaults: vi.fn().mockResolvedValue({
    toolResultTriggerMessages: 16,
    summarizeTriggerTokens: 16000,
    preserveTurns: 8,
    hardTokenLimit: 64000,
    enableSummarization: false,
    summarizerModelId: '',
  }),
  getSandboxLimits: vi.fn(),
  saveAppAgentConfig: vi.fn().mockResolvedValue(undefined),
  publishApp: vi.fn().mockResolvedValue(undefined),
  createDebugSession: vi.fn().mockResolvedValue('s1'),
  getAppSecurity: vi.fn().mockResolvedValue({
    appId: 'a1',
    enabled: false,
    maskToolResult: true,
    maskToolArgs: false,
    maskModelOutput: false,
    maskInput: false,
    rules: [],
    modelOutputRules: [],
  }),
  getMemoryDefaults: vi.fn().mockResolvedValue({ prompt: '平台默认记忆提示词', platformEnabled: true, embeddingModelName: '平台统一向量模型', embeddingDimensions: 1024 }),
  saveAppSecurity: vi.fn().mockResolvedValue(undefined),
  SECURITY_RULE_TYPES: ['phone', 'idCard', 'email', 'bankCard', 'custom'],
  SECURITY_STRATEGIES: ['redact', 'mask', 'hash', 'block', 'flag'],
}))

vi.mock('@/api/agentChat', () => ({
  createAppChatAgent: vi.fn(() => ({})),
  runAppChat: vi.fn().mockResolvedValue(undefined),
  abortAppChat: vi.fn(),
}))

vi.mock('@/api/aichannel', () => ({
  getTeamAvailableModels: vi.fn(),
}))

vi.mock('@/api/team-plugin', () => ({
  getTeamPlugins: vi.fn(),
}))

vi.mock('@/api/wiki', () => ({
  getWikis: vi.fn(),
  getWikiModelOptions: vi.fn().mockResolvedValue({
    embeddingModels: [{ id: 'emb-1', name: '桩向量模型', modelKind: 'embedding' }],
  }),
}))

vi.mock('@/api/memory', () => ({
  clearAppMemory: vi.fn().mockResolvedValue(undefined),
  getAppMemoryStats: vi.fn().mockResolvedValue({ totalMemories: 0, userCount: 0 }),
  getMyMemories: vi.fn().mockResolvedValue({ items: [], total: 0, memoryEnabled: true }),
}))

vi.mock('@/api/knowledgeGraph', () => ({
  getKnowledgeGraphs: vi.fn(),
}))

vi.mock('@/api/skills', () => ({
  getSkillOptions: vi.fn(),
}))

const MODEL_ID = '1c6780ce-2ce5-425f-899e-1d76135cfd81'

const AGENT_DETAIL: AppDetail = {
  appId: 'a1',
  teamId: '3',
  name: '客服助手',
  description: '售前售后问答',
  appType: 'agent',
  avatarPath: '',
  isPublic: true,
  myRole: 2,
}

function renderSection(detail: AppDetail = AGENT_DETAIL, canManage = true) {
  return render(
    <MemoryRouter>
      <AppConfigSection
        teamId={3}
        appId={detail.appId ?? 'a1'}
        detail={detail}
        loading={false}
        canManage={canManage}
      />
    </MemoryRouter>,
  )
}

describe('AppConfigSection（应用配置分区）', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    // vi.clearAllMocks 不会重置 mock 实现，这里显式复位，避免跨用例泄漏
    vi.mocked(getAppAgentConfig).mockResolvedValue({
      appId: 'a1',
      teamId: 3,
      appType: 'agent',
      prompt: '你是客服助手',
      modelId: MODEL_ID,
      wikiIds: [7],
      plugins: ['p1'],
      myRole: 2,
    })
    vi.mocked(getSandboxLimits).mockResolvedValue({
      maxTtlSeconds: 86400,
      maxCpu: '2',
      maxMemory: '2Gi',
    })
    vi.mocked(getTeamAvailableModels).mockResolvedValue([
      { aiModelId: MODEL_ID, name: 'Qwen3.5 9B', modelId: 'qwen/qwen3.5-9b' },
    ] as never)
    vi.mocked(getTeamPlugins).mockResolvedValue({
      teamId: 3,
      myRole: 2,
      canManage: true,
      items: [
        { pluginId: 'p1', pluginName: 'weather', title: '天气查询', kind: 'static', avatarPath: 'public/plugins/p1.png' },
        // 空 Guid 为内存静态插件，无 DB 记录，不可作为绑定目标，应被过滤
        { pluginId: '00000000-0000-0000-0000-000000000000', pluginName: 'memory', title: '内存插件', kind: 'static' },
      ],
    } as never)
    vi.mocked(getWikis).mockResolvedValue({
      teamId: 3,
      myRole: 2,
      items: [
        { wikiId: 7, teamId: 3, name: '产品文档' },
        { wikiId: 8, teamId: 3, name: '运维手册' },
      ],
    })
    vi.mocked(getKnowledgeGraphs).mockResolvedValue({
      teamId: 3,
      myRole: 2,
      enabled: true,
      items: [
        { kgId: 11, teamId: 3, name: '支付域图谱', mode: 'managed' },
        // 接入图不参与应用侧向量检索，不开放绑定，应被过滤
        { kgId: 12, teamId: 3, name: '外部库接入', mode: 'connected' },
      ],
    })
    vi.mocked(getSkillOptions).mockResolvedValue([
      { id: 'sk1', key: 'docx_writer', name: '文档撰写', description: '', isSystem: true, teamId: 0, avatarPath: 'public/skills/sk1.png' },
    ])
    vi.mocked(getApps).mockResolvedValue({
      teamId: 3,
      myRole: 2,
      items: [
        { appId: 'a1', teamId: 3, name: '客服助手', appType: 'agent', publishStatus: 1 },
        { appId: 'wf1', teamId: 3, name: '工单分派流程', appType: 'workflow', publishStatus: 1 },
        // 未发布的流程应用不能绑定为工具，应被过滤
        { appId: 'wf2', teamId: 3, name: '草稿流程', appType: 'workflow', publishStatus: 0 },
        // Agent 应用绑定候选：已发布的其它 Agent 应用（带头像字段）
        { appId: 'a2', teamId: 3, name: '数据分析助手', appType: 'agent', publishStatus: 1, avatarPath: 'public/apps/avatar-a2.png' },
        // 未发布的 Agent 应用不能绑定为工具，应被过滤
        { appId: 'a3', teamId: 3, name: '草稿助手', appType: 'agent', publishStatus: 0 },
      ],
    })
  })

  it('呈现六张收缩卡片（基础配置/用户提示/能力扩展/沙箱/内容脱敏/长期记忆），前四张默认展开、内容脱敏与长期记忆默认收起', async () => {
    renderSection()

    expect(await screen.findByText('基础配置')).toBeTruthy()
    expect(screen.getByText('用户提示')).toBeTruthy()
    expect(screen.getByText('能力扩展')).toBeTruthy()
    expect(screen.getByText('沙箱')).toBeTruthy()
    expect(screen.getByText('内容脱敏')).toBeTruthy()
    expect(screen.getByText('长期记忆')).toBeTruthy()
    expect(document.querySelectorAll('.ant-collapse').length).toBe(6)
    // 「内容脱敏」「长期记忆」为后并入的收缩卡片，默认收起；其余四张默认展开
    const headers = Array.from(document.querySelectorAll('.ant-collapse-header')) as HTMLElement[]
    const collapsedTitles = ['内容脱敏', '长期记忆']
    headers.forEach((node) => {
      const collapsed = collapsedTitles.some((title) => node.textContent?.includes(title))
      expect(node.getAttribute('aria-expanded')).toBe(collapsed ? 'false' : 'true')
    })
    expect(screen.getByText('对话模型')).toBeTruthy()
    expect(screen.getByText('提示词')).toBeTruthy()
    expect(screen.getByText('插件')).toBeTruthy()
    expect(screen.getByText('流程应用')).toBeTruthy()
    expect(screen.getByText('默认技能')).toBeTruthy()
    expect(screen.getByText('知识库')).toBeTruthy()
  })

  it('内容脱敏收缩卡片：展开后显示设置且无独立保存按钮，随「保存设置」一次提交主配置与脱敏设置', async () => {
    renderSection()

    // 默认收起：设置未挂载、未请求（卡片头标题「内容脱敏」展开前唯一）
    const header = (await screen.findByText('内容脱敏')).closest('.ant-collapse-header') as HTMLElement
    expect(screen.queryByText('工具调用结果')).toBeNull()
    expect(getAppSecurity).not.toHaveBeenCalled()

    fireEvent.click(header)
    // 展开后卡片头标题与面板内总开关标签各一个「内容脱敏」（面板异步加载，waitFor 等两者齐）
    await waitFor(() => expect(screen.getAllByText('内容脱敏').length).toBe(2))
    // 面板内容渲染提交与注册 effect 刷出之间有间隙：先刷出注册回调，再点保存（否则本click拿不到回调走纯主配置分支）
    await act(async () => {})
    expect(screen.getByText('工具调用结果')).toBeTruthy()
    expect(screen.getByText('用户输入')).toBeTruthy()

    // 面板不再有独立保存按钮，统一走页面「保存设置」：security 与 agent-config 一次提交
    expect(screen.queryByRole('button', { name: /^保\s*存$/ })).toBeNull()
    fireEvent.click(screen.getByRole('button', { name: /保存设置/ }))
    await waitFor(() => expect(saveAppSecurity).toHaveBeenCalledWith('a1', expect.objectContaining({ enabled: false })))
    await waitFor(() => expect(saveAppAgentConfig).toHaveBeenCalled())
  })

  it('内容脱敏未展开（未注册保存回调）时，「保存设置」仅提交主配置', async () => {
    renderSection()

    expect(await screen.findByText('基础配置')).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: /保存设置/ }))

    await waitFor(() => expect(saveAppAgentConfig).toHaveBeenCalled())
    expect(saveAppSecurity).not.toHaveBeenCalled()
  })

  it('长期记忆收缩卡片：默认收起不挂载，展开后配置随「保存设置」并入主配置 memory 节', async () => {
    renderSection()

    // 默认收起：面板未挂载、无记忆设置内容
    const header = (await screen.findByText('长期记忆')).closest('.ant-collapse-header') as HTMLElement
    expect(header.getAttribute('aria-expanded')).toBe('false')
    expect(screen.queryByText('开启长期记忆')).toBeNull()

    fireEvent.click(header)
    expect(await screen.findByText('开启长期记忆')).toBeTruthy()
    // 面板加载完成即注册 memory 节回调：先刷出注册 effect 再交互
    await act(async () => {})

    // 开启总开关（表单项内定位，避免命中页面其他卡片的开关）；模型/维度统一在系统设置，无应用侧选择控件
    const masterItem = screen.getByText('开启长期记忆').closest('.ant-form-item')
    fireEvent.click(masterItem!.querySelector('button[role="switch"]')!)
    await screen.findByText('向量化模型（系统设置统一配置）')

    // 面板无独立保存/发布按钮，统一走页面「保存设置」：memory 节并入 agent-config 一次提交
    expect(screen.queryByRole('button', { name: /^保\s*存$/ })).toBeNull()
    expect(screen.queryByRole('button', { name: /重新发布/ })).toBeNull()
    fireEvent.click(screen.getByRole('button', { name: /保存设置/ }))
    await waitFor(() =>
      expect(saveAppAgentConfig).toHaveBeenCalledWith(
        'a1',
        expect.objectContaining({
          executionSettings: expect.objectContaining({
            memory: expect.objectContaining({ enabled: true }),
          }),
        }),
      ),
    )
    // 向量化模型/维度统一由系统设置决定：memory 节不携带模型字段
    const memoryPayload = (vi.mocked(saveAppAgentConfig).mock.calls[0][1].executionSettings?.memory ?? {}) as Record<string, unknown>
    expect(memoryPayload.embeddingModelId).toBeUndefined()
    expect(memoryPayload.embeddingDimensions).toBeUndefined()
  })

  it('长期记忆未展开（未注册回调）时保存不携带 memory 节，保持加载时原值', async () => {
    renderSection()

    expect(await screen.findByText('基础配置')).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: /保存设置/ }))

    await waitFor(() => expect(saveAppAgentConfig).toHaveBeenCalled())
    const payload = vi.mocked(saveAppAgentConfig).mock.calls[0][1]
    expect(payload.executionSettings?.memory).toBeUndefined()
  })

  it('收缩卡片可折叠：点击沙箱卡片头收起，再次点击展开', async () => {
    renderSection()

    const header = (await screen.findByText('沙箱')).closest('.ant-collapse-header') as HTMLElement
    expect(header.getAttribute('aria-expanded')).toBe('true')
    fireEvent.click(header)
    expect(header.getAttribute('aria-expanded')).toBe('false')
    fireEvent.click(header)
    expect(header.getAttribute('aria-expanded')).toBe('true')
  })

  it('回显团队可用模型与本团队知识库、已绑定插件', async () => {
    renderSection()

    expect(await screen.findByText('Qwen3.5 9B')).toBeTruthy()
    // 绑定资源名出现在选择器标签与自动放通列表行两处
    expect((await screen.findAllByText('产品文档')).length).toBeGreaterThanOrEqual(1)
    expect(screen.getAllByText('天气查询').length).toBeGreaterThanOrEqual(1)
    await waitFor(() => expect(getTeamAvailableModels).toHaveBeenCalledWith(3))
  })

  it('未选择模型（后端全零 Guid）显示占位符而非裸串，保存按未选择提交', async () => {
    // 新建应用/清空模型时后端回 Guid.Empty 全零串，界面须归一为未选择
    vi.mocked(getAppAgentConfig).mockResolvedValue({
      appId: 'a1',
      teamId: 3,
      appType: 'agent',
      prompt: '你是客服助手',
      modelId: '00000000-0000-0000-0000-000000000000',
      wikiIds: [],
      plugins: [],
      myRole: 2,
    })

    renderSection()

    expect(await screen.findByText('基础配置')).toBeTruthy()
    await waitFor(() => expect(screen.getByText('请选择对话模型')).toBeTruthy())
    expect(screen.queryByText('00000000-0000-0000-0000-000000000000')).toBeNull()

    fireEvent.click(screen.getByRole('button', { name: /保存设置/ }))
    await waitFor(() =>
      expect(saveAppAgentConfig).toHaveBeenCalledWith(
        'a1',
        expect.objectContaining({ modelId: null }),
      ),
    )
  })

  it('开启沙箱后展示存活时间、资源与网络等参数', async () => {
    vi.mocked(getAppAgentConfig).mockResolvedValue({
      appId: 'a1',
      teamId: 3,
      appType: 'agent',
      prompt: '你是客服助手',
      modelId: MODEL_ID,
      wikiIds: [],
      plugins: [],
      myRole: 2,
      executionSettings: {
        sandbox: {
          enabled: true,
          timeoutSeconds: 1200,
          renewOnAccess: false,
          resource: { cpu: '1', memory: '2Gi' },
          network: { defaultAction: 'deny', egress: ['pypi.org'] },
        },
      },
    })

    renderSection()

    expect(await screen.findByText('启用沙箱')).toBeTruthy()
    expect(screen.getByText('沙箱存活时间')).toBeTruthy()
    expect(screen.getByText('CPU 限制')).toBeTruthy()
    expect(screen.getByText('内存限制')).toBeTruthy()
    expect(screen.getByText('出站网络默认动作')).toBeTruthy()
    await waitFor(() => expect(screen.getByDisplayValue('2Gi')).toBeTruthy())
    expect(screen.queryByText('沙箱镜像')).toBeNull()
  })

  it('外部应用不展示技能与沙箱配置，保存固定技能为空、沙箱关闭', async () => {
    vi.mocked(getAppAgentConfig).mockResolvedValue({
      appId: 'a1',
      teamId: 3,
      appType: 'agent',
      prompt: '你是客服助手',
      modelId: MODEL_ID,
      wikiIds: [],
      plugins: [],
      myRole: 2,
      // 存量配置携带已启用的沙箱：外部应用渲染时不展示，保存时固定关闭
      executionSettings: { sandbox: { enabled: true } },
    })

    renderSection({ ...AGENT_DETAIL, isExternal: true })

    expect(await screen.findByText('基础配置')).toBeTruthy()
    expect(screen.getByText(/不支持沙箱与技能/)).toBeTruthy()
    expect(screen.queryByText('默认技能')).toBeNull()
    expect(screen.queryByText('沙箱')).toBeNull()
    expect(screen.queryByText('启用沙箱')).toBeNull()
    // 外部应用面向匿名/外部用户不开放记忆，不渲染「长期记忆」卡片
    expect(screen.queryByText('长期记忆')).toBeNull()
    expect(screen.queryByText('沙箱自动放行')).toBeNull()

    fireEvent.click(screen.getByRole('button', { name: /保存设置/ }))
    await waitFor(() =>
      expect(saveAppAgentConfig).toHaveBeenCalledWith(
        'a1',
        expect.objectContaining({
          skills: [],
          executionSettings: expect.objectContaining({
            sandbox: { enabled: false },
            toolApproval: {
              autoApprovePlugins: [],
              autoApproveWorkflowApps: [],
              autoApproveAgentApps: [],
              autoApproveSkills: [],
              autoApproveWikis: [],
              autoApproveGraphs: [],
              sandboxAutoApproved: false,
            },
          }),
        }),
      ),
    )
  })

  it('提示词、插件、知识库与模型同区保存，一次提交完整配置', async () => {
    renderSection()

    const textarea = await screen.findByPlaceholderText('请输入系统提示词（可选）')
    expect((textarea as HTMLTextAreaElement).value).toBe('你是客服助手')

    fireEvent.change(textarea, { target: { value: '你是售前客服' } })
    fireEvent.click(screen.getByRole('button', { name: /保存设置/ }))

    await waitFor(() =>
      expect(saveAppAgentConfig).toHaveBeenCalledWith('a1', {
        modelId: MODEL_ID,
        prompt: '你是售前客服',
        wikiIds: [7],
        graphIds: [],
        plugins: ['p1'],
        workflowApps: [],
        agentApps: [],
        skills: [],
        openingStatement: '',
        openingStatementEnabled: false,
        quickInputs: [],
        executionSettings: {
          toolResultTriggerMessages: 16,
          summarizeTriggerTokens: 16000,
          preserveTurns: 8,
          hardTokenLimit: 64000,
          enableSummarization: false,
          summarizerModelId: '',
          sandbox: { enabled: false, renewOnAccess: true },
          toolApproval: {
            autoApprovePlugins: [],
            autoApproveWorkflowApps: [],
            autoApproveAgentApps: [],
            autoApproveSkills: [],
            autoApproveWikis: [],
            autoApproveGraphs: [],
            sandboxAutoApproved: false,
          },
          subAgent: { enabled: false },
        },
      }),
    )
  })

  it('审批策略：绑定资源下方生成自动放通列表，回显勾选、行内切换并随保存提交', async () => {
    vi.mocked(getAppAgentConfig).mockResolvedValue({
      appId: 'a1',
      teamId: 3,
      appType: 'agent',
      prompt: '你是客服助手',
      modelId: MODEL_ID,
      wikiIds: [7],
      graphIds: [11],
      plugins: ['p1'],
      workflowApps: ['wf1'],
      agentApps: ['a2'],
      skills: ['sk1'],
      myRole: 2,
      executionSettings: {
        toolApproval: {
          autoApprovePlugins: ['p1'],
          autoApproveWorkflowApps: ['wf1'],
          autoApproveAgentApps: [],
          autoApproveSkills: [],
          autoApproveWikis: [7],
          autoApproveGraphs: [],
          sandboxAutoApproved: true,
        },
      },
    })

    renderSection()

    // 六类绑定资源逐行进入自动放通列表（名称 + 右侧自动放通勾选）；
    // 选择器标签与列表行同名，按「行内含勾选框」唯一定位自动放通行
    const rowOf = (name: string) => {
      const rows = screen
        .getAllByText(name)
        .map((node) => node.closest('div'))
        .filter((div) => div != null && div.querySelector('input[type="checkbox"]') != null)
      expect(rows.length).toBe(1)
      return rows[0]!
    }
    expect((await screen.findAllByText('天气查询')).length).toBeGreaterThanOrEqual(1)
    expect((await screen.findAllByText('工单分派流程')).length).toBeGreaterThanOrEqual(1)
    expect(screen.getAllByText('数据分析助手').length).toBeGreaterThanOrEqual(1)
    expect(screen.getAllByText('文档撰写').length).toBeGreaterThanOrEqual(1)
    expect(screen.getAllByText('产品文档').length).toBeGreaterThanOrEqual(1)
    expect(screen.getAllByText('支付域图谱').length).toBeGreaterThanOrEqual(1)
    // 回显：插件/流程应用/知识库白名单勾选，Agent 应用/技能/图谱未勾选
    const checked = (name: string) => (rowOf(name).querySelector('input[type="checkbox"]') as HTMLInputElement).checked
    expect(checked('天气查询')).toBe(true)
    expect(checked('工单分派流程')).toBe(true)
    expect(checked('产品文档')).toBe(true)
    expect(checked('数据分析助手')).toBe(false)
    expect(checked('文档撰写')).toBe(false)
    expect(checked('支付域图谱')).toBe(false)
    expect(screen.getByText('沙箱自动放行')).toBeTruthy()

    // 行内切换：勾选 Agent 应用、取消知识库勾选
    fireEvent.click(rowOf('数据分析助手').querySelector('input[type="checkbox"]')!)
    fireEvent.click(rowOf('产品文档').querySelector('input[type="checkbox"]')!)

    fireEvent.click(screen.getByRole('button', { name: /保存设置/ }))

    await waitFor(() =>
      expect(saveAppAgentConfig).toHaveBeenCalledWith(
        'a1',
        expect.objectContaining({
          executionSettings: expect.objectContaining({
            toolApproval: {
              autoApprovePlugins: ['p1'],
              autoApproveWorkflowApps: ['wf1'],
              autoApproveAgentApps: ['a2'],
              autoApproveSkills: [],
              autoApproveWikis: [],
              autoApproveGraphs: [],
              sandboxAutoApproved: true,
            },
          }),
        }),
      ),
    )
  })

  it('审批策略：自动放通白名单收敛为本次绑定资源的子集，未绑定项被剔除', async () => {
    vi.mocked(getAppAgentConfig).mockResolvedValue({
      appId: 'a1',
      teamId: 3,
      appType: 'agent',
      prompt: '你是客服助手',
      modelId: MODEL_ID,
      wikiIds: [7],
      plugins: [],
      myRole: 2,
      executionSettings: {
        toolApproval: {
          autoApprovePlugins: ['p1'],
          autoApproveWorkflowApps: ['wf1'],
          autoApproveAgentApps: ['a2'],
          autoApproveSkills: ['sk1'],
          autoApproveWikis: [999],
          autoApproveGraphs: [888],
          sandboxAutoApproved: false,
        },
      },
    })

    renderSection()

    expect(await screen.findByText('基础配置')).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: /保存设置/ }))

    // 未绑定的资源从自动放通白名单中剔除（后端强校验，前端先收敛）
    await waitFor(() =>
      expect(saveAppAgentConfig).toHaveBeenCalledWith(
        'a1',
        expect.objectContaining({
          executionSettings: expect.objectContaining({
            toolApproval: {
              autoApprovePlugins: [],
              autoApproveWorkflowApps: [],
              autoApproveAgentApps: [],
              autoApproveSkills: [],
              autoApproveWikis: [],
              autoApproveGraphs: [],
              sandboxAutoApproved: false,
            },
          }),
        }),
      ),
    )
  })

  it('快捷输入：回显配置内容，编辑后随保存一并提交', async () => {
    vi.mocked(getAppAgentConfig).mockResolvedValue({
      appId: 'a1',
      teamId: 3,
      appType: 'agent',
      prompt: '你是客服助手',
      modelId: MODEL_ID,
      wikiIds: [],
      plugins: [],
      skills: [],
      quickInputs: ['帮我总结一份文档的核心要点', ''],
      myRole: 2,
    })

    renderSection()

    expect(await screen.findByText('快捷输入')).toBeTruthy()
    // 配置中的空串项同样渲染为一行输入框，取第一行编辑
    const inputs = screen.getAllByPlaceholderText('例如：帮我总结一份文档的核心要点') as HTMLInputElement[]
    expect(inputs.length).toBe(2)
    expect(inputs[0].value).toBe('帮我总结一份文档的核心要点')

    // 修改既有项 + 追加一项
    fireEvent.change(inputs[0], { target: { value: '帮我总结文档要点' } })
    fireEvent.click(screen.getByRole('button', { name: /添加快捷输入/ }))
    const afterAdd = screen.getAllByPlaceholderText('例如：帮我总结一份文档的核心要点')
    fireEvent.change(afterAdd[afterAdd.length - 1], { target: { value: '写一段产品介绍' } })

    fireEvent.click(screen.getByRole('button', { name: /保存设置/ }))

    // 空白项被过滤，非空项去首尾空格后提交
    await waitFor(() =>
      expect(saveAppAgentConfig).toHaveBeenCalledWith(
        'a1',
        expect.objectContaining({
          quickInputs: ['帮我总结文档要点', '写一段产品介绍'],
        }),
      ),
    )
  })

  it('对话开场白：回显内容，随保存一并提交', async () => {
    vi.mocked(getAppAgentConfig).mockResolvedValue({
      appId: 'a1',
      teamId: 3,
      appType: 'agent',
      prompt: '你是客服助手',
      modelId: MODEL_ID,
      wikiIds: [],
      plugins: [],
      skills: [],
      openingStatement: '你好，我是售前助手',
      openingStatementEnabled: true,
      myRole: 2,
    })

    renderSection()

    const opening = await screen.findByPlaceholderText('你好，我是你的 AI 助手，有什么可以帮你？')
    expect((opening as HTMLTextAreaElement).value).toBe('你好，我是售前助手')

    fireEvent.change(opening, { target: { value: '你好，很高兴见到你' } })
    fireEvent.click(screen.getByRole('button', { name: /保存设置/ }))

    await waitFor(() =>
      expect(saveAppAgentConfig).toHaveBeenCalledWith(
        'a1',
        expect.objectContaining({
          openingStatement: '你好，很高兴见到你',
          openingStatementEnabled: true,
        }),
      ),
    )
  })

  it('对话开场白：未启用时不展示内容输入框', async () => {
    renderSection()

    expect(await screen.findByText('对话开场白')).toBeTruthy()
    expect(screen.queryByText('开场白内容')).toBeNull()
  })

  it('插件选项只取团队可访问插件：空 Guid 的内存静态插件被过滤', async () => {
    renderSection()
    await screen.findAllByText('天气查询')

    // 七个下拉依次为 对话模型 / 插件 / 流程应用 / Agent 应用 / 默认技能 / 知识库 / 知识图谱
    fireEvent.mouseDown(screen.getAllByRole('combobox')[1])

    await waitFor(() => {
      const options = document.querySelectorAll('.ant-select-item-option')
      const texts = Array.from(options).map((node) => node.textContent ?? '')
      expect(texts.some((text) => text.includes('天气查询'))).toBe(true)
      expect(texts.some((text) => text.includes('内存插件'))).toBe(false)
    })

    // 插件选项以「头像 + 标题」展示：有头像走图片，无头像（内存插件被过滤，仅剩有头像项）回退首字母节点
    expect(document.querySelectorAll('.ant-select-item-option .ant-avatar').length).toBeGreaterThan(0)
    expect(document.querySelectorAll('.ant-select-item-option .ant-avatar img').length).toBeGreaterThan(0)
  })

  it('知识图谱选项只取本团队托管图：接入图不开放绑定，回显与保存携带 graphIds', async () => {
    vi.mocked(getAppAgentConfig).mockResolvedValue({
      appId: 'a1',
      teamId: 3,
      appType: 'agent',
      prompt: '',
      modelId: MODEL_ID,
      wikiIds: [],
      graphIds: [11],
      plugins: [],
      myRole: 2,
    })

    renderSection()
    // 已绑定托管图回显为选中标签与自动放通行
    expect((await screen.findAllByText('支付域图谱')).length).toBeGreaterThanOrEqual(1)

    fireEvent.mouseDown(screen.getAllByRole('combobox')[6])
    await waitFor(() => {
      const options = document.querySelectorAll('.ant-select-item-option')
      const texts = Array.from(options).map((node) => node.textContent ?? '')
      expect(texts.some((text) => text.includes('支付域图谱'))).toBe(true)
      // 接入图不参与应用侧向量检索，不开放绑定
      expect(texts.some((text) => text.includes('外部库接入'))).toBe(false)
    })

    fireEvent.click(screen.getByRole('button', { name: /保存设置/ }))
    await waitFor(() =>
      expect(saveAppAgentConfig).toHaveBeenCalledWith(
        'a1',
        expect.objectContaining({ graphIds: [11] }),
      ),
    )
  })

  it('流程应用选项只取本团队已发布流程应用，回显已绑定项并随保存提交', async () => {
    vi.mocked(getAppAgentConfig).mockResolvedValue({
      appId: 'a1',
      teamId: 3,
      appType: 'agent',
      prompt: '你是客服助手',
      modelId: MODEL_ID,
      wikiIds: [],
      plugins: [],
      workflowApps: ['wf1'],
      myRole: 2,
    })

    renderSection()
    // 已绑定项回显为选中标签与自动放通行
    expect((await screen.findAllByText('工单分派流程')).length).toBeGreaterThanOrEqual(1)

    fireEvent.mouseDown(screen.getAllByRole('combobox')[2])
    await waitFor(() => {
      const options = document.querySelectorAll('.ant-select-item-option')
      const texts = Array.from(options).map((node) => node.textContent ?? '')
      expect(texts.some((text) => text.includes('工单分派流程'))).toBe(true)
      // 未发布流程与 Agent 应用不可绑定
      expect(texts.some((text) => text.includes('草稿流程'))).toBe(false)
      expect(texts.some((text) => text.includes('客服助手'))).toBe(false)
    })

    fireEvent.click(screen.getByRole('button', { name: /保存设置/ }))
    await waitFor(() =>
      expect(saveAppAgentConfig).toHaveBeenCalledWith(
        'a1',
        expect.objectContaining({ workflowApps: ['wf1'] }),
      ),
    )
  })

  it('Agent 应用选项只取本团队已发布的其它 Agent 应用（排除自身），回显已绑定项并随保存提交', async () => {
    vi.mocked(getAppAgentConfig).mockResolvedValue({
      appId: 'a1',
      teamId: 3,
      appType: 'agent',
      prompt: '你是客服助手',
      modelId: MODEL_ID,
      wikiIds: [],
      plugins: [],
      agentApps: ['a2'],
      myRole: 2,
    })

    renderSection()
    // 已绑定项回显为选中标签（带头像）与自动放通行
    expect((await screen.findAllByText('数据分析助手')).length).toBeGreaterThanOrEqual(1)

    // 七个下拉依次为 对话模型 / 插件 / 流程应用 / Agent 应用 / 默认技能 / 知识库 / 知识图谱
    fireEvent.mouseDown(screen.getAllByRole('combobox')[3])
    await waitFor(() => {
      const options = document.querySelectorAll('.ant-select-item-option')
      const texts = Array.from(options).map((node) => node.textContent ?? '')
      expect(texts.some((text) => text.includes('数据分析助手'))).toBe(true)
      // 自身不可绑定（客服助手 = 当前应用 a1），未发布 Agent 与流程应用不在候选内
      expect(texts.some((text) => text.includes('客服助手'))).toBe(false)
      expect(texts.some((text) => text.includes('草稿助手'))).toBe(false)
      expect(texts.some((text) => text.includes('工单分派流程'))).toBe(false)
    })

    // 选项与选中标签均以「头像 + 名称」展示：有头像走图片，无头像回退首字母
    const optionAvatars = document.querySelectorAll('.ant-select-item-option .ant-avatar')
    expect(optionAvatars.length).toBeGreaterThan(0)
    expect(document.querySelectorAll('.ant-select-item-option .ant-avatar img').length).toBeGreaterThan(0)
    expect(document.querySelectorAll('.ant-select-selection-item .ant-avatar').length).toBeGreaterThan(0)

    // 技能多选（第五个下拉）同款头像渲染
    fireEvent.mouseDown(screen.getAllByRole('combobox')[4])
    await waitFor(() => {
      expect(document.querySelectorAll('.ant-select-item-option .ant-avatar img').length).toBeGreaterThan(0)
    })

    fireEvent.click(screen.getByRole('button', { name: /保存设置/ }))
    await waitFor(() =>
      expect(saveAppAgentConfig).toHaveBeenCalledWith(
        'a1',
        expect.objectContaining({ agentApps: ['a2'] }),
      ),
    )
  })

  it('已发布且有未发布配置变更：警告条提供「重新发布」，点击后草稿上线并清除警告', async () => {
    vi.mocked(getAppAgentConfig).mockResolvedValue({
      appId: 'a1',
      teamId: 3,
      appType: 'agent',
      prompt: '你是客服助手',
      modelId: MODEL_ID,
      wikiIds: [],
      plugins: [],
      skills: [],
      status: 0,
      myRole: 2,
    })

    renderSection({ ...AGENT_DETAIL, publishStatus: 1 })

    expect(await screen.findByText(/已有未发布的配置修改/)).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: '重新发布' }))

    await waitFor(() => expect(publishApp).toHaveBeenCalledWith('a1'))
    await waitFor(() => expect(screen.queryByText(/已有未发布的配置修改/)).toBeNull())
  })

  it('流程应用配置区仅提示未开放（基础信息在「信息」分区维护）', async () => {    renderSection({ ...AGENT_DETAIL, appId: 'a2', name: '审批流', description: '', appType: 'workflow', isPublic: false })

    expect(screen.getByText(/流程应用的配置能力尚未开放/)).toBeTruthy()
    expect(screen.queryByText('对话模型')).toBeNull()
    expect(screen.queryByText('提示词')).toBeNull()
    expect(getAppAgentConfig).not.toHaveBeenCalled()
  })

  it('普通成员只读：无保存入口并提示需要团队管理员', async () => {
    renderSection({ ...AGENT_DETAIL, myRole: 0 }, false)

    expect(await screen.findByText('基础配置')).toBeTruthy()
    expect(screen.queryByRole('button', { name: /保存设置/ })).toBeNull()
    expect(screen.getByText(/创建与配置需要团队管理员/)).toBeTruthy()
  })
})

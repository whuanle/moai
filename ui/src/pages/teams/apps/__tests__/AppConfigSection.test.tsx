import { describe, expect, it, vi, beforeEach } from 'vitest'
import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { AppConfigSection, type AppDetail } from '../AppConfigSection'
import { getAppAgentConfig, getAppSecurity, getApps, getSandboxLimits, publishApp, saveAppAgentConfig, saveAppSecurity } from '@/api/app'
import { getTeamGatewayModels } from '@/api/gateway'
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
  getMemoryDefaults: vi.fn().mockResolvedValue({ prompt: '平台默认记忆提示词' }),
  saveAppSecurity: vi.fn().mockResolvedValue(undefined),
  SECURITY_RULE_TYPES: ['phone', 'idCard', 'email', 'bankCard', 'custom'],
  SECURITY_STRATEGIES: ['redact', 'mask', 'hash', 'block', 'flag'],
}))

vi.mock('@/api/agentChat', () => ({
  createAppChatAgent: vi.fn(() => ({})),
  runAppChat: vi.fn().mockResolvedValue(undefined),
  abortAppChat: vi.fn(),
}))

vi.mock('@/api/gateway', () => ({
  getTeamGatewayModels: vi.fn(),
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
    vi.mocked(getTeamGatewayModels).mockResolvedValue([
      { aiModelId: MODEL_ID, name: 'Qwen3.5 9B', modelId: 'qwen/qwen3.5-9b' },
    ] as never)
    vi.mocked(getTeamPlugins).mockResolvedValue({
      teamId: 3,
      myRole: 2,
      canManage: true,
      items: [
        { pluginId: 'p1', pluginName: 'weather', title: '天气查询', kind: 'static' },
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
      { id: 'sk1', key: 'docx_writer', name: '文档撰写', description: '', isSystem: true, teamId: 0 },
    ])
    vi.mocked(getApps).mockResolvedValue({
      teamId: 3,
      myRole: 2,
      items: [
        { appId: 'a1', teamId: 3, name: '客服助手', appType: 'agent', publishStatus: 1 },
        { appId: 'wf1', teamId: 3, name: '工单分派流程', appType: 'workflow', publishStatus: 1 },
        // 未发布的流程应用不能绑定为工具，应被过滤
        { appId: 'wf2', teamId: 3, name: '草稿流程', appType: 'workflow', publishStatus: 0 },
      ],
    })
  })

  it('呈现七张收缩卡片（基础配置/用户提示/能力扩展/沙箱/安全审批/内容脱敏/长期记忆），前五张默认展开、内容脱敏与长期记忆默认收起', async () => {
    renderSection()

    expect(await screen.findByText('基础配置')).toBeTruthy()
    expect(screen.getByText('用户提示')).toBeTruthy()
    expect(screen.getByText('能力扩展')).toBeTruthy()
    expect(screen.getByText('沙箱')).toBeTruthy()
    expect(screen.getByText('安全审批')).toBeTruthy()
    expect(screen.getByText('内容脱敏')).toBeTruthy()
    expect(screen.getByText('长期记忆')).toBeTruthy()
    expect(document.querySelectorAll('.ant-collapse').length).toBe(7)
    // 「内容脱敏」「长期记忆」为后并入的收缩卡片，默认收起；其余五张默认展开
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

    // 开启总开关（表单项内定位，避免命中页面其他卡片的开关）并选定向量化模型
    const masterItem = screen.getByText('开启长期记忆').closest('.ant-form-item')
    fireEvent.click(masterItem!.querySelector('button[role="switch"]')!)
    await screen.findByText('向量化模型')
    fireEvent.mouseDown(screen.getByText('选择该团队可用的向量化模型'))
    fireEvent.click(await screen.findByText('桩向量模型'))

    // 面板无独立保存/发布按钮，统一走页面「保存设置」：memory 节并入 agent-config 一次提交
    expect(screen.queryByRole('button', { name: /^保\s*存$/ })).toBeNull()
    expect(screen.queryByRole('button', { name: /重新发布/ })).toBeNull()
    fireEvent.click(screen.getByRole('button', { name: /保存设置/ }))
    await waitFor(() =>
      expect(saveAppAgentConfig).toHaveBeenCalledWith(
        'a1',
        expect.objectContaining({
          executionSettings: expect.objectContaining({
            memory: expect.objectContaining({ enabled: true, embeddingModelId: 'emb-1' }),
          }),
        }),
      ),
    )
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
    expect(screen.getByText('产品文档')).toBeTruthy()
    expect(screen.getByText('天气查询')).toBeTruthy()
    await waitFor(() => expect(getTeamGatewayModels).toHaveBeenCalledWith(3))
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
            toolApproval: { autoApprovePlugins: [], sandboxAutoApproved: false },
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
          toolApproval: { autoApprovePlugins: [], sandboxAutoApproved: false },
          subAgent: { enabled: false },
        },
      }),
    )
  })

  it('审批策略：回显自动放行插件与沙箱开关，随保存一并提交', async () => {
    vi.mocked(getAppAgentConfig).mockResolvedValue({
      appId: 'a1',
      teamId: 3,
      appType: 'agent',
      prompt: '你是客服助手',
      modelId: MODEL_ID,
      wikiIds: [7],
      plugins: ['p1'],
      myRole: 2,
      executionSettings: {
        toolApproval: { autoApprovePlugins: ['p1'], sandboxAutoApproved: true },
      },
    })

    renderSection()

    expect(await screen.findByText('安全审批')).toBeTruthy()
    expect(screen.getByText('沙箱自动放行')).toBeTruthy()
    expect(screen.getByText('自动放行插件')).toBeTruthy()

    fireEvent.click(screen.getByRole('button', { name: /保存设置/ }))

    await waitFor(() =>
      expect(saveAppAgentConfig).toHaveBeenCalledWith(
        'a1',
        expect.objectContaining({
          executionSettings: expect.objectContaining({
            toolApproval: { autoApprovePlugins: ['p1'], sandboxAutoApproved: true },
          }),
        }),
      ),
    )
  })

  it('审批策略：自动放行插件收敛为本次绑定插件的子集，未绑定项被剔除', async () => {
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
        toolApproval: { autoApprovePlugins: ['p1'], sandboxAutoApproved: false },
      },
    })

    renderSection()

    expect(await screen.findByText('安全审批')).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: /保存设置/ }))

    // 未绑定的插件从自动放行白名单中剔除（后端强校验，前端先收敛）
    await waitFor(() =>
      expect(saveAppAgentConfig).toHaveBeenCalledWith(
        'a1',
        expect.objectContaining({
          executionSettings: expect.objectContaining({
            toolApproval: { autoApprovePlugins: [], sandboxAutoApproved: false },
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
    await screen.findByText('天气查询')

    // 六个下拉依次为 对话模型 / 插件 / 流程应用 / 默认技能 / 知识库 / 知识图谱
    fireEvent.mouseDown(screen.getAllByRole('combobox')[1])

    await waitFor(() => {
      const options = document.querySelectorAll('.ant-select-item-option')
      const texts = Array.from(options).map((node) => node.textContent ?? '')
      expect(texts.some((text) => text.includes('天气查询'))).toBe(true)
      expect(texts.some((text) => text.includes('内存插件'))).toBe(false)
    })
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
    // 已绑定托管图回显为选中标签
    expect(await screen.findByText('支付域图谱')).toBeTruthy()

    fireEvent.mouseDown(screen.getAllByRole('combobox')[5])
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
    // 已绑定项回显为选中标签
    expect(await screen.findByText('工单分派流程')).toBeTruthy()

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

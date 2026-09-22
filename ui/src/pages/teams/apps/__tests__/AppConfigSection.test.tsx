import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { AppConfigSection, type AppDetail } from '../AppConfigSection'
import { getAppAgentConfig, getApps, getSandboxLimits, publishApp, saveAppAgentConfig } from '@/api/app'
import { getTeamGatewayModels } from '@/api/gateway'
import { getTeamPlugins } from '@/api/team-plugin'
import { getSkillOptions } from '@/api/skills'
import { getWikis } from '@/api/wiki'
import { getKnowledgeGraphs } from '@/api/knowledgeGraph'

vi.mock('@/api/app', () => ({
  getAppDetail: vi.fn(),
  getAppAgentConfig: vi.fn(),
  getApps: vi.fn(),
  getSandboxLimits: vi.fn(),
  saveAppAgentConfig: vi.fn().mockResolvedValue(undefined),
  publishApp: vi.fn().mockResolvedValue(undefined),
  createDebugSession: vi.fn().mockResolvedValue('s1'),
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

  it('呈现 Agent 配置表单（模型/提示词/插件/流程应用/默认技能/知识库）', async () => {
    renderSection()

    expect(await screen.findByText('Agent 配置')).toBeTruthy()
    expect(screen.getByText('对话模型')).toBeTruthy()
    expect(screen.getByText('提示词')).toBeTruthy()
    expect(screen.getByText('插件')).toBeTruthy()
    expect(screen.getByText('流程应用')).toBeTruthy()
    expect(screen.getByText('默认技能')).toBeTruthy()
    expect(screen.getByText('知识库')).toBeTruthy()
  })

  it('回显团队可用模型与本团队知识库、已绑定插件', async () => {
    renderSection()

    expect(await screen.findByText('Qwen3.5 9B')).toBeTruthy()
    expect(screen.getByText('产品文档')).toBeTruthy()
    expect(screen.getByText('天气查询')).toBeTruthy()
    await waitFor(() => expect(getTeamGatewayModels).toHaveBeenCalledWith(3))
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

    expect(await screen.findByText('Agent 配置')).toBeTruthy()
    expect(screen.getByText(/不支持沙箱与技能/)).toBeTruthy()
    expect(screen.queryByText('默认技能')).toBeNull()
    expect(screen.queryByText('启用沙箱')).toBeNull()
    expect(screen.queryByText('沙箱自动放行')).toBeNull()

    fireEvent.click(screen.getByRole('button', { name: /保存配置/ }))
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
    fireEvent.click(screen.getByRole('button', { name: /保存配置/ }))

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
          sandbox: { enabled: false, renewOnAccess: true },
          toolApproval: { autoApprovePlugins: [], sandboxAutoApproved: false },
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

    expect(await screen.findByText('审批策略')).toBeTruthy()
    expect(screen.getByText('沙箱自动放行')).toBeTruthy()
    expect(screen.getByText('自动放行插件')).toBeTruthy()

    fireEvent.click(screen.getByRole('button', { name: /保存配置/ }))

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

    expect(await screen.findByText('审批策略')).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: /保存配置/ }))

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

    fireEvent.click(screen.getByRole('button', { name: /保存配置/ }))

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
    fireEvent.click(screen.getByRole('button', { name: /保存配置/ }))

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

    fireEvent.click(screen.getByRole('button', { name: /保存配置/ }))
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

    fireEvent.click(screen.getByRole('button', { name: /保存配置/ }))
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

    expect(await screen.findByText('Agent 配置')).toBeTruthy()
    expect(screen.queryByRole('button', { name: /保存配置/ })).toBeNull()
    expect(screen.getByText(/创建与配置需要团队管理员/)).toBeTruthy()
  })
})

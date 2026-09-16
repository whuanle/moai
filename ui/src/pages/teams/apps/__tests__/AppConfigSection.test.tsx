import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { AppConfigSection, type AppDetail } from '../AppConfigSection'
import { getAppAgentConfig, saveAppAgentConfig } from '@/api/app'
import { getTeamGatewayModels } from '@/api/gateway'
import { getTeamPlugins } from '@/api/team-plugin'
import { getSkillOptions } from '@/api/skills'
import { getWikis } from '@/api/wiki'

vi.mock('@/api/app', () => ({
  getAppDetail: vi.fn(),
  getAppAgentConfig: vi.fn(),
  saveAppAgentConfig: vi.fn().mockResolvedValue(undefined),
  updateApp: vi.fn().mockResolvedValue(undefined),
  uploadAppAvatar: vi.fn().mockResolvedValue(''),
  createDebugSession: vi.fn().mockResolvedValue('s1'),
}))

vi.mock('@/api/agentChat', () => ({
  createAppChatAgent: vi.fn(() => ({})),
  runAppChat: vi.fn().mockResolvedValue(undefined),
  abortAppChat: vi.fn(),
}))

vi.mock('@/api/publication', () => ({
  applyPublication: vi.fn().mockResolvedValue('1'),
  getTeamPublicationList: vi.fn().mockResolvedValue([]),
  withdrawPublication: vi.fn().mockResolvedValue(undefined),
  reviewPublication: vi.fn().mockResolvedValue(undefined),
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
        onReload={vi.fn()}
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
    vi.mocked(getSkillOptions).mockResolvedValue([
      { id: 'sk1', key: 'docx_writer', name: '文档撰写', description: '', isSystem: true, teamId: 0 },
    ])
  })

  it('左栏应用信息与右栏 Agent 配置同时呈现', async () => {
    renderSection()

    expect(await screen.findByText('应用信息')).toBeTruthy()
    expect(screen.getByText('Agent 配置')).toBeTruthy()
    expect(screen.getByText('对话模型')).toBeTruthy()
    expect(screen.getByText('提示词')).toBeTruthy()
    expect(screen.getByText('插件')).toBeTruthy()
    expect(screen.getByText('知识库')).toBeTruthy()
    await waitFor(() => expect((screen.getByLabelText('应用名称') as HTMLInputElement).value).toBe('客服助手'))
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
        plugins: ['p1'],
        skills: [],
        executionSettings: { sandbox: { enabled: false, renewOnAccess: true } },
      }),
    )
  })

  it('插件选项只取团队可访问插件：空 Guid 的内存静态插件被过滤', async () => {
    renderSection()
    await screen.findByText('天气查询')

    // 四个下拉依次为 对话模型 / 插件 / 技能 / 知识库
    fireEvent.mouseDown(screen.getAllByRole('combobox')[1])

    await waitFor(() => {
      const options = document.querySelectorAll('.ant-select-item-option')
      const texts = Array.from(options).map((node) => node.textContent ?? '')
      expect(texts.some((text) => text.includes('天气查询'))).toBe(true)
      expect(texts.some((text) => text.includes('内存插件'))).toBe(false)
    })
  })

  it('流程应用只保留应用信息，配置区提示未开放', async () => {
    renderSection({ ...AGENT_DETAIL, appId: 'a2', name: '审批流', description: '', appType: 'workflow', isPublic: false })

    expect(await screen.findByText('应用信息')).toBeTruthy()
    expect(screen.queryByText('对话模型')).toBeNull()
    expect(screen.queryByText('提示词')).toBeNull()
    expect(screen.getByText(/流程应用的配置能力尚未开放/)).toBeTruthy()
    expect(getAppAgentConfig).not.toHaveBeenCalled()
  })

  it('普通成员只读：无保存入口并提示需要团队管理员', async () => {
    renderSection({ ...AGENT_DETAIL, myRole: 0 }, false)

    expect(await screen.findByText('应用信息')).toBeTruthy()
    expect(screen.queryByRole('button', { name: /保存信息/ })).toBeNull()
    expect(screen.queryByRole('button', { name: /保存配置/ })).toBeNull()
    expect(screen.getByText(/创建与配置需要团队管理员/)).toBeTruthy()
  })
})

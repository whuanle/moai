import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { AppMemorySettingsPanel } from '../AppMemoryPanel'
import { getAppAgentConfig, getMemoryDefaults, saveAppAgentConfig } from '@/api/app'
import { clearAppMemory, getAppMemoryStats, getMyMemories } from '@/api/memory'

vi.mock('@/api/app', () => ({
  getAppAgentConfig: vi.fn(),
  getMemoryDefaults: vi.fn().mockResolvedValue({
    prompt: '平台默认记忆提示词：如何管理与使用记忆',
    platformEnabled: true,
    embeddingModelName: '平台统一向量模型',
    embeddingDimensions: 1024,
  }),
  saveAppAgentConfig: vi.fn().mockResolvedValue(undefined),
}))

vi.mock('@/api/memory', () => ({
  clearAppMemory: vi.fn().mockResolvedValue(undefined),
  getAppMemoryStats: vi.fn().mockResolvedValue({ totalMemories: 0, userCount: 0 }),
  getMyMemories: vi.fn().mockResolvedValue({ items: [], total: 0, memoryEnabled: true }),
}))

vi.mock('react-i18next', async (importOriginal) => ({
  ...(await importOriginal<object>()),
  useTranslation: () => ({ t: (key: string, params?: Record<string, unknown>) => {
    if (params && 'count' in params) return `${key}:${params.count}`
    if (params && 'dims' in params) return `${key}:${params.dims}`
    return key
  } }),
}))

const mockConfig = (executionSettings: Record<string, unknown>) => {
  vi.mocked(getAppAgentConfig).mockResolvedValue({
    appId: 'a1',
    teamId: 3,
    appType: 'agent',
    prompt: 'p',
    modelId: 'm1',
    wikiIds: [],
    plugins: [],
    myRole: 2,
    executionSettings,
  } as never)
}

/** 嵌入态宿主桩：捕获面板注册的 memory 节构造回调 */
function renderPanel(canManage = true) {
  const state = { getSettings: null as (() => Record<string, unknown>) | null }
  const registerSettings = (get: (() => Record<string, unknown>) | null) => {
    state.getSettings = get
  }
  render(<AppMemorySettingsPanel teamId={3} appId="a1" canManage={canManage} registerSettings={registerSettings} />)
  return state
}

/** 展开总开关（平台默认已开启长期记忆） */
async function enableMemory() {
  fireEvent.click(screen.getByRole('switch'))
  await screen.findByText('appMemory.platformModelLabel')
}

describe('AppMemorySettingsPanel（长期记忆设置面板，「配置」分区嵌入态）', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('加载完成后注册 memory 节构造回调；未开启时仅展示总开关，开启后级联显示统一模型回显等配置', async () => {
    mockConfig({})
    const state = renderPanel()
    expect(await screen.findByText('appMemory.enabled')).toBeTruthy()
    // 配置回显完成后才注册，防宿主在回显前保存默认值
    await waitFor(() => expect(state.getSettings).toBeTruthy())
    expect(screen.queryByText('appMemory.platformModelLabel')).toBeFalsy()
    await enableMemory()
    expect(screen.getByText('appMemory.prompt')).toBeTruthy()
    expect(screen.getByText('appMemory.autoExtract')).toBeTruthy()
    // 向量化模型/维度统一由系统设置决定：只读回显平台配置，应用侧不提供选择控件
    expect(screen.getByText('平台统一向量模型')).toBeTruthy()
    expect(screen.getByText('appMemory.platformDimensions:1024')).toBeTruthy()
  })

  it('注册回调仅产出 memory 节且不含模型/维度字段（保存主配置时并入 executionSettings），面板不渲染保存/发布按钮', async () => {
    mockConfig({ memory: { enabled: true, embeddingModelId: 'emb-1', embeddingDimensions: 1024, retrievalTopK: 5 } })
    const state = renderPanel()
    await screen.findByText('appMemory.prompt')
    await waitFor(() => expect(state.getSettings).toBeTruthy())

    fireEvent.click(screen.getAllByRole('switch')[0])
    const settings = state.getSettings!()
    expect(settings).toEqual({ enabled: false })
    // 关闭态不携带开启态专属参数，也不携带模型/维度（统一在系统设置）

    fireEvent.click(screen.getAllByRole('switch')[0])
    const enabledSettings = state.getSettings!()
    expect(enabledSettings).toMatchObject({
      enabled: true,
      retrievalTopK: 5,
      autoExtract: true,
    })
    expect('embeddingModelId' in enabledSettings).toBe(false)
    expect('embeddingDimensions' in enabledSettings).toBe(false)
    // 记忆类型完全由 AI 自行判定：配置不再下发类型限制字段
    expect('enableSemantic' in enabledSettings).toBe(false)
    expect('enableEpisodic' in enabledSettings).toBe(false)
    expect('enableProcedural' in enabledSettings).toBe(false)

    // 嵌入态不渲染自己的保存按钮（宿主「保存设置」统一提交），也不再有发布动作
    expect(screen.queryByRole('button', { name: /appManage.saveConfig/ })).toBeFalsy()
    expect(screen.queryByRole('button', { name: /appManage.republish/ })).toBeFalsy()
    expect(saveAppAgentConfig).not.toHaveBeenCalled()
  })

  it('平台未开启长期记忆时总开关禁用并提示', async () => {
    vi.mocked(getAppAgentConfig).mockResolvedValue({
      appId: 'a1',
      teamId: 3,
      appType: 'agent',
      prompt: 'p',
      modelId: 'm1',
      wikiIds: [],
      plugins: [],
      myRole: 2,
      executionSettings: {},
    } as never)
    vi.mocked(getMemoryDefaults).mockResolvedValue({
      prompt: '平台默认记忆提示词：如何管理与使用记忆',
      platformEnabled: false,
      embeddingModelName: '',
      embeddingDimensions: 1024,
    })
    renderPanel()
    await screen.findByText('appMemory.enabled')
    const sw = screen.getByRole('switch') as HTMLButtonElement
    expect(sw.disabled).toBe(true)
    expect(screen.getByText('appMemory.platformOffHint')).toBeTruthy()
  })

  it('记忆提示词：留空时平台默认展示为占位符且不随回调下发，配置后回显并随回调产出', async () => {
    // 未配置 prompt：占位符展示平台默认
    mockConfig({ memory: { enabled: true, embeddingModelId: 'emb-1', embeddingDimensions: 1024 } })
    const state = renderPanel()
    await screen.findByText('appMemory.prompt')
    expect(screen.getByPlaceholderText('平台默认记忆提示词：如何管理与使用记忆')).toBeTruthy()
    await waitFor(() => expect(state.getSettings).toBeTruthy())
    expect('prompt' in state.getSettings!()).toBe(false)

    // 配置 prompt：回显并随回调下发
    mockConfig({ memory: { enabled: true, embeddingModelId: 'emb-1', prompt: '优先记录用户的项目背景与偏好' } })
    const state2 = renderPanel()
    const textarea = await screen.findByDisplayValue('优先记录用户的项目背景与偏好') as HTMLTextAreaElement
    expect(state2.getSettings).toBeTruthy()
    await waitFor(() => expect(state2.getSettings!()).toMatchObject({ prompt: '优先记录用户的项目背景与偏好' }))
    expect(textarea).toBeTruthy()
  })

  it('清空应用全部记忆需 Popconfirm 确认且调用管理端接口', async () => {
    mockConfig({ memory: { enabled: true, embeddingModelId: 'emb-1', embeddingDimensions: 1024 } })
    vi.mocked(getMyMemories).mockResolvedValue({
      items: [{ id: 'mem-1', content: '测试记忆', memoryType: 0, createTime: '2026-10-12', updateTime: '2026-10-12' }],
      total: 1,
      memoryEnabled: true,
    })
    renderPanel()
    await screen.findByText('appMemory.clearApp')
    fireEvent.click(screen.getByText('appMemory.clearApp'))
    const okButtons = await screen.findAllByRole('button', { name: /OK/i })
    fireEvent.click(okButtons[okButtons.length - 1])
    await waitFor(() => expect(clearAppMemory).toHaveBeenCalledWith('a1'))
  })

  it('展示应用记忆统计（总条数与用户数），清空后归零', async () => {
    mockConfig({ memory: { enabled: true, embeddingModelId: 'emb-1', embeddingDimensions: 1024 } })
    vi.mocked(getAppMemoryStats).mockResolvedValue({ totalMemories: 7, userCount: 3 })
    renderPanel()
    expect(await screen.findByText('appMemory.statsHint:7')).toBeInTheDocument()
    fireEvent.click(screen.getByText('appMemory.clearApp'))
    const okButtons = await screen.findAllByRole('button', { name: /OK/i })
    fireEvent.click(okButtons[okButtons.length - 1])
    await waitFor(() => expect(screen.getByText('appMemory.statsEmpty')).toBeInTheDocument())
  })

  it('只读成员无清空入口', async () => {
    mockConfig({})
    render(<AppMemorySettingsPanel teamId={3} appId="a1" canManage={false} />)
    await screen.findByText('appMemory.enabled')
    expect(screen.queryByText('appMemory.clearApp')).toBeFalsy()
  })

  it('允许用户接入全局记忆：开关随开启态展示并产出 allowGlobalMemory 节点', async () => {
    mockConfig({ memory: { enabled: true, embeddingModelId: 'emb-1', embeddingDimensions: 1024 } })
    const state = renderPanel()
    await screen.findByText('appMemory.autoExtract')
    await waitFor(() => expect(state.getSettings).toBeTruthy())

    // 未显式配置时默认关闭；开关顺序：enabled=[0]，autoExtract=[1]，allowGlobalMemory=[2]
    expect(state.getSettings!()).toMatchObject({ enabled: true, allowGlobalMemory: false })
    fireEvent.click(screen.getAllByRole('switch')[2])
    expect(state.getSettings!()).toMatchObject({ enabled: true, allowGlobalMemory: true })
  })
})

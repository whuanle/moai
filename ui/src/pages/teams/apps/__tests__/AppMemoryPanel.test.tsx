import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { AppMemorySettingsPanel } from '../AppMemoryPanel'
import { getAppAgentConfig, saveAppAgentConfig } from '@/api/app'
import { clearAppMemory, getAppMemoryStats, getMyMemories } from '@/api/memory'

vi.mock('@/api/app', () => ({
  getAppAgentConfig: vi.fn(),
  getMemoryDefaults: vi.fn().mockResolvedValue({ prompt: '平台默认记忆提示词：如何管理与使用记忆' }),
  saveAppAgentConfig: vi.fn().mockResolvedValue(undefined),
}))

vi.mock('@/api/wiki', () => ({
  getWikiModelOptions: vi.fn().mockResolvedValue({
    embeddingModels: [{ id: 'emb-1', name: '桩向量模型', modelKind: 'embedding' }],
  }),
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

/** 展开总开关并选定向量化模型 */
async function enableMemoryWithModel() {
  fireEvent.click(screen.getByRole('switch'))
  await screen.findByText('appMemory.embeddingModel')
  fireEvent.mouseDown(screen.getByText('appMemory.embeddingModelPlaceholder'))
  const option = await screen.findByText('桩向量模型')
  fireEvent.click(option)
}

describe('AppMemorySettingsPanel（长期记忆设置面板，「配置」分区嵌入态）', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('加载完成后注册 memory 节构造回调；未开启时仅展示总开关，开启后级联显示向量化模型等配置', async () => {
    mockConfig({})
    const state = renderPanel()
    expect(await screen.findByText('appMemory.enabled')).toBeTruthy()
    // 配置回显完成后才注册，防宿主在回显前保存默认值
    await waitFor(() => expect(state.getSettings).toBeTruthy())
    expect(screen.queryByText('appMemory.embeddingModel')).toBeFalsy()
    await enableMemoryWithModel()
    expect(screen.getByText('appMemory.embeddingDimensions')).toBeTruthy()
    expect(screen.getByText('appMemory.autoExtract')).toBeTruthy()
  })

  it('注册回调仅产出 memory 节（保存主配置时并入 executionSettings），面板不渲染保存/发布按钮', async () => {
    mockConfig({ memory: { enabled: true, embeddingModelId: 'emb-1', embeddingDimensions: 1024, retrievalTopK: 5 } })
    const state = renderPanel()
    await screen.findByText('appMemory.embeddingModel')
    await waitFor(() => expect(state.getSettings).toBeTruthy())

    fireEvent.click(screen.getAllByRole('switch')[0])
    const settings = state.getSettings!()
    expect(settings).toEqual({ enabled: false, embeddingModelId: 'emb-1', embeddingDimensions: 1024 })
    // 关闭期间保留模型/维度锁定参照系，丢弃开启态专属参数

    fireEvent.click(screen.getAllByRole('switch')[0])
    const enabledSettings = state.getSettings!()
    expect(enabledSettings).toMatchObject({
      enabled: true,
      embeddingModelId: 'emb-1',
      embeddingDimensions: 1024,
      retrievalTopK: 5,
      autoExtract: true,
    })
    // 记忆类型完全由 AI 自行判定：配置不再下发类型限制字段
    expect('enableSemantic' in enabledSettings).toBe(false)
    expect('enableEpisodic' in enabledSettings).toBe(false)
    expect('enableProcedural' in enabledSettings).toBe(false)

    // 嵌入态不渲染自己的保存按钮（宿主「保存设置」统一提交），也不再有发布动作
    expect(screen.queryByRole('button', { name: /appManage.saveConfig/ })).toBeFalsy()
    expect(screen.queryByRole('button', { name: /appManage.republish/ })).toBeFalsy()
    expect(saveAppAgentConfig).not.toHaveBeenCalled()
  })

  it('开启但未选向量化模型时回调抛错中断宿主保存', async () => {
    mockConfig({})
    const state = renderPanel()
    await screen.findByText('appMemory.enabled')
    await waitFor(() => expect(state.getSettings).toBeTruthy())
    fireEvent.click(screen.getByRole('switch'))
    expect(() => state.getSettings!()).toThrow()
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

  it('应用内已有记忆时向量化模型与维度锁定，清空全部记忆后解锁', async () => {
    mockConfig({ memory: { enabled: true, embeddingModelId: 'emb-1', embeddingDimensions: 1024 } })
    vi.mocked(getAppMemoryStats).mockResolvedValue({ totalMemories: 5, userCount: 2 })
    renderPanel()
    await screen.findByText('appMemory.embeddingModel')
    expect(document.querySelectorAll('.ant-select-disabled').length).toBeGreaterThan(0)
    expect(document.querySelector('.ant-input-number-disabled')).toBeTruthy()
    fireEvent.click(screen.getByText('appMemory.clearApp'))
    const okButtons = await screen.findAllByRole('button', { name: /OK/i })
    fireEvent.click(okButtons[okButtons.length - 1])
    await waitFor(() => expect(document.querySelector('.ant-select-disabled')).toBeNull())
  })

  it('只读成员无清空入口', async () => {
    mockConfig({})
    render(<AppMemorySettingsPanel teamId={3} appId="a1" canManage={false} />)
    await screen.findByText('appMemory.enabled')
    expect(screen.queryByText('appMemory.clearApp')).toBeFalsy()
  })
})

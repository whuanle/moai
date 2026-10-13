import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { AppMemorySection } from '../AppMemorySection'
import { getAppAgentConfig, publishApp, saveAppAgentConfig } from '@/api/app'
import { getWikiModelOptions } from '@/api/wiki'
import { clearAppMemory, getAppMemoryStats, getMyMemories } from '@/api/memory'

vi.mock('@/api/app', () => ({
  getAppAgentConfig: vi.fn(),
  publishApp: vi.fn().mockResolvedValue(undefined),
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

/** 展开总开关并选定向量化模型（保存前置条件） */
async function enableMemoryWithModel() {
  fireEvent.click(screen.getByRole('switch'))
  await screen.findByText('appMemory.embeddingModel')
  fireEvent.mouseDown(screen.getByText('appMemory.embeddingModelPlaceholder'))
  const option = await screen.findByText('桩向量模型')
  fireEvent.click(option)
}

describe('AppMemorySection（应用长期记忆分区）', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('未开启记忆时仅展示总开关，开启后级联显示向量化模型等配置', async () => {
    mockConfig({})
    render(<MemoryRouter><AppMemorySection teamId={3} appId="a1" canManage isPublished={false} /></MemoryRouter>)
    expect(await screen.findByText('appMemory.enabled')).toBeTruthy()
    expect(screen.queryByText('appMemory.embeddingModel')).toBeFalsy()
    await enableMemoryWithModel()
    expect(screen.getByText('appMemory.embeddingDimensions')).toBeTruthy()
    expect(screen.getByText('appMemory.autoExtract')).toBeTruthy()
  })

  it('保存时全量回传配置快照并仅改 memory 节', async () => {
    mockConfig({ sandbox: { enabled: true }, toolApproval: { sandboxAutoApproved: false } })
    render(<MemoryRouter><AppMemorySection teamId={3} appId="a1" canManage isPublished={false} /></MemoryRouter>)
    await screen.findByText('appMemory.enabled')
    await enableMemoryWithModel()
    fireEvent.change(screen.getByPlaceholderText('1024'), { target: { value: '768' } })
    fireEvent.click(screen.getByRole('button', { name: /appManage.saveConfig/ }))
    await waitFor(() => expect(saveAppAgentConfig).toHaveBeenCalled())
    const payload = vi.mocked(saveAppAgentConfig).mock.calls[0][1]
    expect(payload.executionSettings?.memory).toMatchObject({ enabled: true, embeddingModelId: 'emb-1', embeddingDimensions: 768 })
    // 其他分区配置原样保留
    expect(payload.executionSettings?.sandbox).toEqual({ enabled: true })
    expect(payload.modelId).toBe('m1')
  })

  it('未选向量化模型时保存被拦截，不发起请求', async () => {
    mockConfig({})
    render(<MemoryRouter><AppMemorySection teamId={3} appId="a1" canManage isPublished={false} /></MemoryRouter>)
    await screen.findByText('appMemory.enabled')
    // 仅开启不选模型
    fireEvent.click(screen.getByRole('switch'))
    await screen.findByText('appMemory.embeddingModel')
    fireEvent.click(screen.getByRole('button', { name: /appManage.saveConfig/ }))
    await waitFor(() => expect(saveAppAgentConfig).not.toHaveBeenCalled())
  })

  it('清空应用全部记忆需 Popconfirm 确认且调用管理端接口', async () => {
    mockConfig({ memory: { enabled: true, embeddingModelId: 'emb-1', embeddingDimensions: 1024 } })
    vi.mocked(getMyMemories).mockResolvedValue({
      items: [{ id: 'mem-1', content: '测试记忆', memoryType: 0, createTime: '2026-10-12', updateTime: '2026-10-12' }],
      total: 1,
      memoryEnabled: true,
    })
    render(<MemoryRouter><AppMemorySection teamId={3} appId="a1" canManage isPublished={false} /></MemoryRouter>)
    await screen.findByText('appMemory.clearApp')
    fireEvent.click(screen.getByText('appMemory.clearApp'))
    const okButtons = await screen.findAllByRole('button', { name: /OK/i })
    fireEvent.click(okButtons[okButtons.length - 1])
    await waitFor(() => expect(clearAppMemory).toHaveBeenCalledWith('a1'))
    expect(publishApp).not.toHaveBeenCalled()
  })

  it('展示应用记忆统计（总条数与用户数），清空后归零', async () => {
    mockConfig({ memory: { enabled: true, embeddingModelId: 'emb-1', embeddingDimensions: 1024 } })
    vi.mocked(getAppMemoryStats).mockResolvedValue({ totalMemories: 7, userCount: 3 })
    render(<MemoryRouter><AppMemorySection teamId={3} appId="a1" canManage isPublished={false} /></MemoryRouter>)
    expect(await screen.findByText('appMemory.statsHint:7')).toBeInTheDocument()
    fireEvent.click(screen.getByText('appMemory.clearApp'))
    const okButtons = await screen.findAllByRole('button', { name: /OK/i })
    fireEvent.click(okButtons[okButtons.length - 1])
    await waitFor(() => expect(screen.getByText('appMemory.statsEmpty')).toBeInTheDocument())
  })

  it('应用内已有记忆时向量化模型与维度锁定，清空全部记忆后解锁', async () => {
    mockConfig({ memory: { enabled: true, embeddingModelId: 'emb-1', embeddingDimensions: 1024 } })
    vi.mocked(getAppMemoryStats).mockResolvedValue({ totalMemories: 5, userCount: 2 })
    render(<MemoryRouter><AppMemorySection teamId={3} appId="a1" canManage isPublished={false} /></MemoryRouter>)
    await screen.findByText('appMemory.embeddingModel')
    expect(document.querySelectorAll('.ant-select-disabled').length).toBeGreaterThan(0)
    expect(document.querySelector('.ant-input-number-disabled')).toBeTruthy()
    fireEvent.click(screen.getByText('appMemory.clearApp'))
    const okButtons = await screen.findAllByRole('button', { name: /OK/i })
    fireEvent.click(okButtons[okButtons.length - 1])
    await waitFor(() => expect(document.querySelector('.ant-select-disabled')).toBeNull())
  })

  it('只读成员无保存与清空入口', async () => {
    mockConfig({})
    render(<MemoryRouter><AppMemorySection teamId={3} appId="a1" canManage={false} isPublished={false} /></MemoryRouter>)
    await screen.findByText('appManage.memberHint')
    expect(screen.queryByRole('button', { name: /appManage.saveConfig/ })).toBeFalsy()
    expect(screen.queryByText('appMemory.clearApp')).toBeFalsy()
  })
})

import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { MemoryManagement } from '../MemoryManagement'
import {
  clearMyGlobalMemory,
  deleteMyGlobalMemory,
  deleteMyMemory,
  getMyGlobalMemories,
  getMyMemories,
  getMyMemoryOverview,
} from '@/api/memory'

vi.mock('@/api/memory', () => ({
  clearMyAppMemory: vi.fn().mockResolvedValue(undefined),
  clearMyGlobalMemory: vi.fn().mockResolvedValue(undefined),
  deleteMyGlobalMemory: vi.fn().mockResolvedValue(undefined),
  deleteMyMemory: vi.fn().mockResolvedValue(undefined),
  getMyGlobalMemories: vi.fn().mockResolvedValue({ items: [], total: 0, globalEnabled: false }),
  getMyMemories: vi.fn().mockResolvedValue({ items: [], total: 0, memoryEnabled: true }),
  getMyMemoryOverview: vi.fn().mockResolvedValue({ apps: [], global: { enabled: false, myCount: 0 } }),
}))

vi.mock('@/store/app', () => ({
  useAppStore: (selector: (state: { userInfo?: { isRoot?: boolean } }) => unknown) =>
    selector({ userInfo: { isRoot: false } }),
}))

vi.mock('react-i18next', async (importOriginal) => ({
  ...(await importOriginal<object>()),
  useTranslation: () => ({ t: (key: string, params?: Record<string, unknown>) => {
    if (params && 'count' in params) return `${key}:${params.count}`
    return key
  } }),
}))

const mockOverview = (apps: unknown[], global: { enabled: boolean; myCount: number }) => {
  vi.mocked(getMyMemoryOverview).mockResolvedValue({
    apps: apps as never,
    global: global as never,
  })
}

describe('MemoryManagement', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockOverview(
      [{ appId: 'app-1', appName: '客服机器人', teamName: '团队A', memoryCount: 2, lastActiveTime: '2026-01-01T00:00:00Z' }],
      { enabled: false, myCount: 0 },
    )
    vi.mocked(getMyMemories).mockResolvedValue({
      items: [
        { id: 'm1', appId: 'app-1', scope: 'app', memoryType: 0, content: '用户偏好简洁回复', updateTime: '2026-01-01T00:00:00Z' },
      ],
      total: 1,
      memoryEnabled: true,
    } as never)
  })

  it('应用记忆 tab：总览列出应用并可进入明细', async () => {
    render(<MemoryManagement />)
    expect(await screen.findByText('客服机器人')).toBeInTheDocument()
    expect(screen.getByText('团队A')).toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: 'memoryMgmt.view' }))
    expect(await screen.findByText('用户偏好简洁回复')).toBeInTheDocument()
    expect(vi.mocked(getMyMemories)).toHaveBeenCalledWith('app-1', 1, 20, undefined)
  })

  it('应用记忆 tab：工具栏关键字前端过滤应用列表', async () => {
    mockOverview(
      [
        { appId: 'app-1', appName: '客服机器人', teamName: '团队A', memoryCount: 2, lastActiveTime: '2026-01-01T00:00:00Z' },
        { appId: 'app-2', appName: '翻译助手', teamName: '团队B', memoryCount: 1, lastActiveTime: '2026-01-02T00:00:00Z' },
      ],
      { enabled: false, myCount: 0 },
    )

    render(<MemoryManagement />)
    expect(await screen.findByText('客服机器人')).toBeInTheDocument()
    expect(screen.getByText('翻译助手')).toBeInTheDocument()

    // 原提示文案已移除，工具栏为搜索框
    expect(screen.queryByText('memoryMgmt.appsHint')).not.toBeInTheDocument()
    fireEvent.change(screen.getByPlaceholderText('memoryMgmt.searchAppsPlaceholder'), { target: { value: '客服' } })

    expect(screen.getByText('客服机器人')).toBeInTheDocument()
    expect(screen.queryByText('翻译助手')).not.toBeInTheDocument()
    // 按团队名也可命中
    fireEvent.change(screen.getByPlaceholderText('memoryMgmt.searchAppsPlaceholder'), { target: { value: '团队b' } })
    expect(screen.getByText('翻译助手')).toBeInTheDocument()
    expect(screen.queryByText('客服机器人')).not.toBeInTheDocument()
  })

  it('应用记忆明细：仅可删除（走删除接口），无编辑/存为全局入口', async () => {
    render(<MemoryManagement />)
    fireEvent.click(await screen.findByRole('button', { name: 'memoryMgmt.view' }))
    expect(await screen.findByText('用户偏好简洁回复')).toBeInTheDocument()

    // 人工维护入口已全部下线：不存在编辑/新建/存为全局按钮
    expect(screen.queryByRole('button', { name: /memoryMgmt\.edit/ })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /memoryMgmt\.promote/ })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /memoryMgmt\.create/ })).not.toBeInTheDocument()

    // 删除走删除接口（Popconfirm 确认）
    fireEvent.click(screen.getByRole('button', { name: /memoryMgmt\.delete/ }))
    fireEvent.click(await screen.findByRole('button', { name: 'common.ok' }))
    await waitFor(() => {
      expect(deleteMyMemory).toHaveBeenCalledWith('app-1', 'm1')
    })
  })

  it('全局记忆未开启：无解说文案，仅空表格且无新建入口', async () => {
    render(<MemoryManagement />)
    fireEvent.click(await screen.findByRole('tab', { name: 'memoryMgmt.tabGlobal:0' }))
    // 页签内容渲染完成（工具栏搜索框在）
    expect(await screen.findByPlaceholderText('memoryMgmt.searchGlobalPlaceholder')).toBeInTheDocument()
    // 不再有整段解说式提示文案
    expect(screen.queryByText('memoryMgmt.globalDisabled')).not.toBeInTheDocument()
    expect(screen.queryByText('memoryMgmt.globalDisabledRoot')).not.toBeInTheDocument()
    expect(screen.queryByText('memoryMgmt.globalHint')).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /memoryMgmt\.create/ })).not.toBeInTheDocument()
  })

  it('全局记忆开启：仅可删除（走全局删除接口），无新建/编辑入口', async () => {
    mockOverview([], { enabled: true, myCount: 1 })
    vi.mocked(getMyGlobalMemories).mockResolvedValue({
      items: [
        { id: 'g1', scope: 'global', memoryType: 1, content: '全局事实：用户在杭州办公', updateTime: '2026-01-01T00:00:00Z' },
      ],
      total: 1,
      globalEnabled: true,
    } as never)

    render(<MemoryManagement />)
    fireEvent.click(await screen.findByRole('tab', { name: 'memoryMgmt.tabGlobal:1' }))
    expect(await screen.findByText('全局事实：用户在杭州办公')).toBeInTheDocument()

    // 人工维护入口已全部下线
    expect(screen.queryByRole('button', { name: /memoryMgmt\.create/ })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /memoryMgmt\.edit/ })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /memoryMgmt\.promote/ })).not.toBeInTheDocument()

    // 删除走全局删除接口（Popconfirm 确认）
    fireEvent.click(screen.getByRole('button', { name: /memoryMgmt\.delete/ }))
    fireEvent.click(await screen.findByRole('button', { name: 'common.ok' }))
    await waitFor(() => {
      expect(deleteMyGlobalMemory).toHaveBeenCalledWith('g1')
    })
  })

  it('全局记忆搜索：输入后点「查询」按钮按关键字重查', async () => {
    mockOverview([], { enabled: true, myCount: 1 })
    vi.mocked(getMyGlobalMemories).mockResolvedValue({
      items: [
        { id: 'g1', scope: 'global', memoryType: 0, content: '全局事实：用户在杭州办公', updateTime: '2026-01-01T00:00:00Z' },
      ],
      total: 1,
      globalEnabled: true,
    } as never)

    render(<MemoryManagement />)
    fireEvent.click(await screen.findByRole('tab', { name: 'memoryMgmt.tabGlobal:1' }))
    await screen.findByText('全局事实：用户在杭州办公')

    fireEvent.change(screen.getByPlaceholderText('memoryMgmt.searchGlobalPlaceholder'), { target: { value: '杭州' } })
    fireEvent.click(screen.getByRole('button', { name: 'memoryMgmt.search' }))
    await waitFor(() => {
      expect(getMyGlobalMemories).toHaveBeenCalledWith(1, 20, '杭州')
    })
  })

  it('全局记忆工具栏：仅保留清空按钮（Popconfirm 确认后走清空接口）', async () => {
    mockOverview([], { enabled: true, myCount: 1 })
    vi.mocked(getMyGlobalMemories).mockResolvedValue({
      items: [
        { id: 'g1', scope: 'global', memoryType: 0, content: '全局事实：用户在杭州办公', updateTime: '2026-01-01T00:00:00Z' },
      ],
      total: 1,
      globalEnabled: true,
    } as never)

    render(<MemoryManagement />)
    fireEvent.click(await screen.findByRole('tab', { name: 'memoryMgmt.tabGlobal:1' }))
    await screen.findByText('全局事实：用户在杭州办公')

    fireEvent.click(screen.getByRole('button', { name: 'memoryMgmt.clear' }))
    fireEvent.click(await screen.findByRole('button', { name: 'common.ok' }))
    await waitFor(() => {
      expect(clearMyGlobalMemory).toHaveBeenCalled()
    })
  })
})

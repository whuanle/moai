import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { MemoryManagement } from '../MemoryManagement'
import {
  deleteMyGlobalMemory,
  getMyGlobalMemories,
  getMyMemories,
  getMyMemoryOverview,
  promoteMemoryToGlobal,
  saveMyGlobalMemory,
  updateMyGlobalMemory,
  updateMyMemory,
} from '@/api/memory'

vi.mock('@/api/memory', () => ({
  clearMyAppMemory: vi.fn().mockResolvedValue(undefined),
  clearMyGlobalMemory: vi.fn().mockResolvedValue(undefined),
  deleteMyGlobalMemory: vi.fn().mockResolvedValue(undefined),
  deleteMyMemory: vi.fn().mockResolvedValue(undefined),
  getMyGlobalMemories: vi.fn().mockResolvedValue({ items: [], total: 0, globalEnabled: false }),
  getMyMemories: vi.fn().mockResolvedValue({ items: [], total: 0, memoryEnabled: true }),
  getMyMemoryOverview: vi.fn().mockResolvedValue({ apps: [], global: { enabled: false, myCount: 0 } }),
  promoteMemoryToGlobal: vi.fn().mockResolvedValue({ id: 'g1' }),
  saveMyGlobalMemory: vi.fn().mockResolvedValue({ id: 'g2' }),
  updateMyGlobalMemory: vi.fn().mockResolvedValue(undefined),
  updateMyMemory: vi.fn().mockResolvedValue(undefined),
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
    expect(vi.mocked(getMyMemories)).toHaveBeenCalledWith('app-1', 1, 20)
  })

  it('应用记忆明细：编辑调用更新接口', async () => {
    render(<MemoryManagement />)
    fireEvent.click(await screen.findByRole('button', { name: 'memoryMgmt.view' }))

    fireEvent.click(await screen.findByRole('button', { name: 'memoryMgmt.edit' }))
    const textarea = await screen.findByRole('textbox')
    fireEvent.change(textarea, { target: { value: '用户偏好极简回复' } })
    fireEvent.click(screen.getByRole('button', { name: 'memoryMgmt.save' }))

    await waitFor(() => {
      expect(updateMyMemory).toHaveBeenCalledWith('app-1', 'm1', '用户偏好极简回复', 0)
    })
  })

  it('全局记忆未开启：展示引导提示且不可新建', async () => {
    render(<MemoryManagement />)
    fireEvent.click(await screen.findByRole('tab', { name: 'memoryMgmt.tabGlobal:0' }))
    expect(await screen.findByText('memoryMgmt.globalDisabled')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /memoryMgmt\.create/ })).toBeDisabled()
  })

  it('全局记忆开启：新建与删除走全局接口', async () => {
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

    // 新建全局记忆（按钮内图标带 aria-label，按正则匹配可访问名）
    fireEvent.click(screen.getByRole('button', { name: /memoryMgmt\.create/ }))
    const textarea = await screen.findByRole('textbox')
    fireEvent.change(textarea, { target: { value: '用户习惯使用暗色主题' } })
    fireEvent.click(screen.getByRole('button', { name: 'memoryMgmt.save' }))
    await waitFor(() => {
      expect(saveMyGlobalMemory).toHaveBeenCalledWith('用户习惯使用暗色主题', 0)
    })

    // 编辑全局记忆
    fireEvent.click(screen.getAllByRole('button', { name: 'memoryMgmt.edit' })[0])
    const editBox = await screen.findByRole('textbox')
    fireEvent.change(editBox, { target: { value: '全局事实：用户在杭州远程办公' } })
    fireEvent.click(screen.getByRole('button', { name: 'memoryMgmt.save' }))
    await waitFor(() => {
      expect(updateMyGlobalMemory).toHaveBeenCalledWith('g1', '全局事实：用户在杭州远程办公', 1)
    })

    // 删除全局记忆（Popconfirm 确认）
    fireEvent.click(screen.getAllByRole('button', { name: 'memoryMgmt.delete' })[0])
    fireEvent.click(await screen.findByRole('button', { name: 'common.ok' }))
    await waitFor(() => {
      expect(deleteMyGlobalMemory).toHaveBeenCalledWith('g1')
    })
  })

  it('应用记忆可存为全局（平台开启后可用）', async () => {
    mockOverview(
      [{ appId: 'app-1', appName: '客服机器人', teamName: '团队A', memoryCount: 1, lastActiveTime: '2026-01-01T00:00:00Z' }],
      { enabled: true, myCount: 0 },
    )

    render(<MemoryManagement />)
    fireEvent.click(await screen.findByRole('button', { name: 'memoryMgmt.view' }))

    fireEvent.click(await screen.findByRole('button', { name: 'memoryMgmt.promote' }))
    await waitFor(() => {
      expect(promoteMemoryToGlobal).toHaveBeenCalledWith('app-1', 'm1')
    })
  })
})

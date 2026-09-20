import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { AppPlaza } from '../AppPlaza'
import { getPublicApps } from '@/api/app'

vi.mock('@/api/app', () => ({
  getPublicApps: vi.fn(),
}))

describe('AppPlaza（应用广场）', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('展示平台公开应用', async () => {
    vi.mocked(getPublicApps).mockResolvedValue([
      {
        appId: '01924f5e-0000-7000-8000-0000000000f1',
        teamId: 1,
        name: '公开助手',
        description: '平台可用',
        appType: 'agent',
        avatarPath: '',
        isPublic: true,
        publishStatus: 1,
      },
    ])

    render(
      <MemoryRouter>
        <AppPlaza />
      </MemoryRouter>,
    )

    expect(await screen.findByText('公开助手')).toBeTruthy()
    expect(screen.getByText('进入对话')).toBeTruthy()
    await waitFor(() => expect(getPublicApps).toHaveBeenCalled())
  })

  it('已发布流程应用展示进入对话入口', async () => {
    vi.mocked(getPublicApps).mockResolvedValue([
      {
        appId: '01924f5e-0000-7000-8000-0000000000f2',
        teamId: 2,
        name: '流程演示',
        description: '已发布流程应用',
        appType: 'workflow',
        avatarPath: '',
        isPublic: true,
        publishStatus: 1,
      },
    ])

    render(
      <MemoryRouter>
        <AppPlaza />
      </MemoryRouter>,
    )

    expect(await screen.findByText('流程演示')).toBeTruthy()
    expect(screen.getByText('流程应用')).toBeTruthy()
    // 公开列表只含已发布应用：流程应用同样开放对话入口（一轮对话 = 一次已发布流程执行）
    expect(screen.getByText('进入对话')).toBeTruthy()
  })

  it('无公开应用时展示空状态', async () => {
    vi.mocked(getPublicApps).mockResolvedValue([])

    render(
      <MemoryRouter>
        <AppPlaza />
      </MemoryRouter>,
    )

    expect(await screen.findByText('平台暂无公开应用')).toBeTruthy()
  })
})

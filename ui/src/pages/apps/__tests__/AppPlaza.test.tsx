import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { AppPlaza } from '../AppPlaza'
import { favoriteApp, getPublicApps, unfavoriteApp } from '@/api/app'

vi.mock('@/api/classify', () => ({
  classifyApi: { getClassifies: vi.fn().mockResolvedValue([]) },
  classifyLabel: (c: { emoji?: string | null; name?: string | null }) => [c?.emoji, c?.name].filter(Boolean).join(' '),
  ClassifyType: { Plugin: 'plugin', App: 'app', Kb: 'kb', Prompt: 'prompt', Skill: 'skill' },
}))
vi.mock('@/api/app', () => ({
  getPublicApps: vi.fn(),
  favoriteApp: vi.fn(),
  unfavoriteApp: vi.fn(),
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

  it('收藏态渲染：已收藏实心星、未收藏空心星', async () => {
    vi.mocked(getPublicApps).mockResolvedValue([
      {
        appId: '01924f5e-0000-7000-8000-0000000000f1',
        teamId: 1,
        name: '已收藏应用',
        description: '收藏置顶',
        appType: 'agent',
        avatarPath: '',
        isPublic: true,
        publishStatus: 1,
        isFavorite: true,
      },
      {
        appId: '01924f5e-0000-7000-8000-0000000000f2',
        teamId: 1,
        name: '未收藏应用',
        description: '普通热度序',
        appType: 'agent',
        avatarPath: '',
        isPublic: true,
        publishStatus: 1,
        isFavorite: false,
      },
    ])

    render(
      <MemoryRouter>
        <AppPlaza />
      </MemoryRouter>,
    )

    expect(await screen.findByText('已收藏应用')).toBeTruthy()
    // aria-label 承载收藏态语义：已收藏=取消收藏（实心）、未收藏=收藏（空心）
    expect(screen.getByRole('button', { name: '取消收藏' })).toBeTruthy()
    expect(screen.getByRole('button', { name: '收藏' })).toBeTruthy()
  })

  it('点击空心星调收藏接口并静默刷新列表', async () => {
    const user = userEvent.setup()
    vi.mocked(getPublicApps).mockResolvedValue([
      {
        appId: '01924f5e-0000-7000-8000-0000000000f2',
        teamId: 1,
        name: '未收藏应用',
        description: '普通热度序',
        appType: 'agent',
        avatarPath: '',
        isPublic: true,
        publishStatus: 1,
      },
    ])
    vi.mocked(favoriteApp).mockResolvedValue(undefined)

    render(
      <MemoryRouter>
        <AppPlaza />
      </MemoryRouter>,
    )

    await screen.findByText('未收藏应用')
    await user.click(screen.getByRole('button', { name: '收藏' }))

    await waitFor(() => expect(favoriteApp).toHaveBeenCalledWith('01924f5e-0000-7000-8000-0000000000f2'))
    expect(unfavoriteApp).not.toHaveBeenCalled()
    // 收藏切换后静默刷新（getPublicApps 至少被再次调用）
    await waitFor(() => expect(vi.mocked(getPublicApps).mock.calls.length).toBeGreaterThanOrEqual(2))
  })

  it('点击实心星调取消收藏接口', async () => {
    const user = userEvent.setup()
    vi.mocked(getPublicApps).mockResolvedValue([
      {
        appId: '01924f5e-0000-7000-8000-0000000000f1',
        teamId: 1,
        name: '已收藏应用',
        description: '收藏置顶',
        appType: 'agent',
        avatarPath: '',
        isPublic: true,
        publishStatus: 1,
        isFavorite: true,
      },
    ])
    vi.mocked(unfavoriteApp).mockResolvedValue(undefined)

    render(
      <MemoryRouter>
        <AppPlaza />
      </MemoryRouter>,
    )

    await screen.findByText('已收藏应用')
    await user.click(screen.getByRole('button', { name: '取消收藏' }))

    await waitFor(() => expect(unfavoriteApp).toHaveBeenCalledWith('01924f5e-0000-7000-8000-0000000000f1'))
    expect(favoriteApp).not.toHaveBeenCalled()
  })

  it('点击查询按钮按关键字重新拉取列表', async () => {
    const user = userEvent.setup()
    vi.mocked(getPublicApps).mockResolvedValue([])

    render(
      <MemoryRouter>
        <AppPlaza />
      </MemoryRouter>,
    )

    await screen.findByText('平台暂无公开应用')
    await user.type(screen.getByPlaceholderText('搜索应用名称/描述'), '助手')
    // antd Button 两汉字自动插空格（查 询）且可能带 loading 图标文本，用正则匹配可访问名
    await user.click(screen.getByRole('button', { name: /查\s*询/ }))

    await waitFor(() =>
      expect(vi.mocked(getPublicApps)).toHaveBeenLastCalledWith({ keywords: '助手', classifyId: undefined }),
    )
  })
})

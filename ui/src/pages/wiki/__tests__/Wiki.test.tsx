import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { Wiki } from '../Wiki'
import { useAppStore } from '@/store/app'
import { getWikis } from '@/api/wiki'
import { getMyTeams } from '@/api/team'

vi.mock('@/api/wiki', () => ({
  getWikis: vi.fn(),
  getWikiDetail: vi.fn(),
  createWiki: vi.fn().mockResolvedValue(3),
  updateWiki: vi.fn().mockResolvedValue(undefined),
  deleteWiki: vi.fn().mockResolvedValue(undefined),
}))

vi.mock('@/api/team', () => ({
  getMyTeams: vi.fn().mockResolvedValue([]),
}))

vi.mock('@/api/kiota', () => ({
  getApiClient: vi.fn(() => ({})),
}))

function renderWiki() {
  return render(
    <MemoryRouter>
      <Wiki />
    </MemoryRouter>,
  )
}

describe('Wiki（一级菜单只读卡片列表）', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    useAppStore.setState({
      userInfo: { accessToken: 'token', userId: '1', userName: 'owner' },
      currentTeamId: null,
      myTeams: [
        { teamId: '7', name: 'Alpha', myRole: 2 },
        { teamId: '8', name: 'Beta', myRole: 0 },
      ],
    })
  })

  it('聚合我的团队下的知识库为卡片列表', async () => {
    vi.mocked(getWikis).mockImplementation(async (teamId: number) => {
      if (teamId === 7) {
        return { teamId: '7', myRole: 2, items: [
          { wikiId: '1', teamId: '7', name: '产品文档', description: '产品相关', isPublic: true, createTime: '2026-09-02T00:00:00Z' },
        ] }
      }
      return { teamId: '8', myRole: 0, items: [
        { wikiId: '2', teamId: '8', name: '技术文档', description: '', isPublic: false, createTime: '2026-09-02T00:00:00Z' },
      ] }
    })

    renderWiki()

    expect(await screen.findByText('产品文档')).toBeInTheDocument()
    expect(screen.getByText('技术文档')).toBeInTheDocument()
    expect(getWikis).toHaveBeenCalledWith(7)
    expect(getWikis).toHaveBeenCalledWith(8)
  })

  it('myTeams 为空时回退到 getMyTeams 并聚合', async () => {
    useAppStore.setState({ myTeams: [] })
    vi.mocked(getMyTeams).mockResolvedValue([{ teamId: '7', name: 'Alpha', myRole: 2 }])
    vi.mocked(getWikis).mockResolvedValue({ teamId: '7', myRole: 2, items: [
      { wikiId: '1', teamId: '7', name: '知识库', description: '', isPublic: false, createTime: '2026-09-02T00:00:00Z' },
    ] })

    renderWiki()

    expect(await screen.findByText('知识库')).toBeInTheDocument()
    expect(getMyTeams).toHaveBeenCalled()
  })

  it('只读：不显示新建/编辑/删除按钮（即使有 Owner 团队）', async () => {
    useAppStore.setState({ myTeams: [{ teamId: '7', name: 'Alpha', myRole: 2 }] })
    vi.mocked(getWikis).mockResolvedValue({ teamId: '7', myRole: 2, items: [
      { wikiId: '1', teamId: '7', name: '产品文档', description: '', isPublic: false, createTime: '2026-09-02T00:00:00Z' },
    ] })

    renderWiki()
    expect(await screen.findByText('产品文档')).toBeInTheDocument()

    expect(screen.queryByRole('button', { name: /新建知识库/ })).toBeNull()
    expect(screen.queryByLabelText('编辑')).toBeNull()
    expect(screen.queryByLabelText('删除')).toBeNull()
  })

  it('卡片显示公开标识与团队名', async () => {
    useAppStore.setState({ myTeams: [{ teamId: '7', name: 'Alpha', myRole: 2 }] })
    vi.mocked(getWikis).mockResolvedValue({ teamId: '7', myRole: 2, items: [
      { wikiId: '1', teamId: '7', name: '产品文档', description: '', isPublic: true, createTime: '2026-09-02T00:00:00Z' },
    ] })

    renderWiki()

    expect(await screen.findByText('产品文档')).toBeInTheDocument()
    expect(screen.getByLabelText('公开')).toBeInTheDocument()
    expect(screen.getByText('Alpha')).toBeInTheDocument()
  })

  it('点击卡片可进入知识库详情页（存在可点击跳转容器）', async () => {
    useAppStore.setState({ myTeams: [{ teamId: '7', name: 'Alpha', myRole: 2 }] })
    vi.mocked(getWikis).mockResolvedValue({ teamId: '7', myRole: 2, items: [
      { wikiId: '1', teamId: '7', name: '产品文档', description: '', isPublic: false, createTime: '2026-09-02T00:00:00Z' },
    ] })

    renderWiki()
    expect(await screen.findByText('产品文档')).toBeInTheDocument()

    const card = screen.getByText('产品文档')
    expect(card).toBeInTheDocument()
    await waitFor(() => expect(card).toBeInTheDocument())
  })
})

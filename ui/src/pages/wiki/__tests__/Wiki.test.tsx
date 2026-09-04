import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { Wiki } from '../Wiki'
import { useAppStore } from '@/store/app'
import { getWikis } from '@/api/wiki'

vi.mock('@/api/wiki', () => ({
  getWikis: vi.fn().mockResolvedValue({
    teamId: '7',
    myRole: 0,
    items: [
      { wikiId: '1', teamId: '7', name: '产品文档', description: '产品相关', createTime: '2026-09-02T00:00:00Z' },
      { wikiId: '2', teamId: '7', name: '技术文档', description: '', createTime: '2026-09-02T00:00:00Z' },
    ],
  }),
  getWikiDetail: vi.fn(),
  createWiki: vi.fn().mockResolvedValue(3),
  updateWiki: vi.fn().mockResolvedValue(undefined),
  deleteWiki: vi.fn().mockResolvedValue(undefined),
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

describe('Wiki', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(getWikis).mockResolvedValue({
      teamId: '7',
      myRole: 0,
      items: [
        { wikiId: '1', teamId: '7', name: '产品文档', description: '产品相关', createTime: '2026-09-02T00:00:00Z' },
        { wikiId: '2', teamId: '7', name: '技术文档', description: '', createTime: '2026-09-02T00:00:00Z' },
      ],
    })
    useAppStore.setState({
      userInfo: { accessToken: 'token', userId: '1', userName: 'owner' },
      currentTeamId: '7',
      myTeams: [{ teamId: '7', name: 'Alpha', myRole: 0 }],
    })
  })

  it('渲染当前团队的知识库列表', async () => {
    renderWiki()

    await waitFor(() => {
      expect(getWikis).toHaveBeenCalledWith(7)
    })
    expect(await screen.findByText('产品文档')).toBeInTheDocument()
    expect(screen.getByText('技术文档')).toBeInTheDocument()
  })

  it('Admin 角色显示新建与操作列', async () => {
    renderWiki()
    expect(await screen.findByText('产品文档')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: '新建知识库' })).toBeInTheDocument()
    const actionButtons = Array.from(
      document.querySelectorAll('table button[aria-label]'),
    ).map((b) => b.getAttribute('aria-label'))
    expect(actionButtons).toContain('编辑')
    expect(actionButtons).toContain('删除')
  })

  it('未选择团队时提示先选团队', async () => {
    useAppStore.setState({ currentTeamId: null })
    renderWiki()
    expect(screen.getByText(/请先在左上角选择一个团队/)).toBeInTheDocument()
    expect(getWikis).not.toHaveBeenCalled()
  })

  it('Member 角色不显示新建与操作列', async () => {
    vi.mocked(getWikis).mockResolvedValueOnce({ teamId: '7', myRole: 2, items: [] })
    renderWiki()
    await waitFor(() => {
      expect(getWikis).toHaveBeenCalled()
    })
    await waitFor(() => {
      expect(screen.queryByRole('button', { name: '新建知识库' })).toBeNull()
    })
  })
})

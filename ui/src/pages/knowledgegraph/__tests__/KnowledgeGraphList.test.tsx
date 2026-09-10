import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { KnowledgeGraphList } from '../KnowledgeGraphList'
import { useAppStore } from '@/store/app'
import { getKnowledgeGraphs } from '@/api/knowledgeGraph'

vi.mock('@/api/knowledgeGraph', () => ({
  getKnowledgeGraphs: vi.fn(),
}))
vi.mock('@/api/team', () => ({ getMyTeams: vi.fn().mockResolvedValue([]) }))
vi.mock('@/api/kiota', () => ({ getApiClient: vi.fn(() => ({})) }))

function renderPage() {
  return render(<MemoryRouter><KnowledgeGraphList /></MemoryRouter>)
}

describe('KnowledgeGraphList', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    useAppStore.setState({
      userInfo: { accessToken: 't', userId: '1', userName: 'o' },
      currentTeamId: null,
      myTeams: [{ teamId: '7', name: 'Alpha', myRole: 2 }],
    })
  })

  it('聚合团队下的知识图谱卡片', async () => {
    vi.mocked(getKnowledgeGraphs).mockResolvedValue({
      teamId: '7', myRole: 2, enabled: true,
      items: [{ kgId: '1', teamId: '7', name: '支付域图谱', description: '运维' }],
    })
    renderPage()
    expect(await screen.findByText('支付域图谱')).toBeInTheDocument()
    expect(getKnowledgeGraphs).toHaveBeenCalledWith(7)
  })

  it('未开启能力时显示提示', async () => {
    vi.mocked(getKnowledgeGraphs).mockResolvedValue({ teamId: '7', myRole: 2, enabled: false, items: [] })
    renderPage()
    expect(await screen.findByText(/未开启知识图谱能力/)).toBeInTheDocument()
  })
})

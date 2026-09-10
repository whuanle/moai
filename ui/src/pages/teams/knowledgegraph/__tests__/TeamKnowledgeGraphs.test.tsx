import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { TeamKnowledgeGraphs } from '../TeamKnowledgeGraphs'
import { getKnowledgeGraphs, getKnowledgeGraphTemplates } from '@/api/knowledgeGraph'

vi.mock('@/api/knowledgeGraph', () => ({
  getKnowledgeGraphs: vi.fn(),
  getKnowledgeGraphTemplates: vi.fn().mockResolvedValue([]),
  createKnowledgeGraph: vi.fn().mockResolvedValue(1),
  updateKnowledgeGraph: vi.fn().mockResolvedValue(undefined),
  deleteKnowledgeGraph: vi.fn().mockResolvedValue(undefined),
}))

vi.mock('@/api/kiota', () => ({ getApiClient: vi.fn(() => ({})) }))

function renderTeam() {
  return render(
    <MemoryRouter>
      <TeamKnowledgeGraphs teamId={7} />
    </MemoryRouter>,
  )
}

describe('TeamKnowledgeGraphs', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(getKnowledgeGraphTemplates).mockResolvedValue([
      { key: 'blank', name: '空白 / 自定义' },
      { key: 'ops', name: '运维服务', entityTypes: ['服务', '人员'] },
    ])
  })

  it('加载并展示团队知识图谱，管理员可见新建入口', async () => {
    vi.mocked(getKnowledgeGraphs).mockResolvedValue({
      teamId: '7',
      myRole: 2,
      enabled: true,
      items: [{ kgId: '1', teamId: '7', name: '支付域图谱', description: '运维' }],
    })

    renderTeam()

    expect(await screen.findByText('支付域图谱')).toBeInTheDocument()
    expect(getKnowledgeGraphs).toHaveBeenCalledWith(7)
    expect(screen.getByRole('button', { name: /新建知识图谱/ })).toBeInTheDocument()
  })

  it('普通成员不显示新建入口', async () => {
    vi.mocked(getKnowledgeGraphs).mockResolvedValue({
      teamId: '7',
      myRole: 0,
      enabled: true,
      items: [{ kgId: '1', teamId: '7', name: '支付域图谱' }],
    })

    renderTeam()

    expect(await screen.findByText('支付域图谱')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /新建知识图谱/ })).toBeNull()
  })

  it('未开启能力时提示禁用且新建按钮不可用', async () => {
    vi.mocked(getKnowledgeGraphs).mockResolvedValue({ teamId: '7', myRole: 2, enabled: false, items: [] })

    renderTeam()

    expect(await screen.findByText(/未开启知识图谱能力/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /新建知识图谱/ })).toBeDisabled()
  })
})

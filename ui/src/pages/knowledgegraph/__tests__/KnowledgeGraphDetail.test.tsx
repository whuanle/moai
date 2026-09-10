import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router'
import '@/i18n'
import { KnowledgeGraphDetail } from '../KnowledgeGraphDetail'
import { getKnowledgeGraphDetail } from '@/api/knowledgeGraph'

vi.mock('@/api/knowledgeGraph', () => ({
  getKnowledgeGraphDetail: vi.fn(),
  getKnowledgeGraphSchema: vi.fn().mockResolvedValue({ entityTypes: [], relationTypes: [] }),
  getKnowledgeGraphNodes: vi.fn().mockResolvedValue({ items: [], total: 0 }),
  getKnowledgeGraphEdges: vi.fn().mockResolvedValue({ items: [], total: 0 }),
  createKnowledgeGraphNode: vi.fn(),
  updateKnowledgeGraphNode: vi.fn(),
  deleteKnowledgeGraphNode: vi.fn(),
  createKnowledgeGraphEdge: vi.fn(),
  updateKnowledgeGraphEdge: vi.fn(),
  deleteKnowledgeGraphEdge: vi.fn(),
  createEntityType: vi.fn(),
  updateEntityType: vi.fn(),
  deleteEntityType: vi.fn(),
  createRelationType: vi.fn(),
  updateRelationType: vi.fn(),
  deleteRelationType: vi.fn(),
  updateKnowledgeGraph: vi.fn(),
  deleteKnowledgeGraph: vi.fn(),
}))

vi.mock('@/api/kiota', () => ({ getApiClient: vi.fn(() => ({})) }))

function renderShell(initialEntry = '/team/7/kg/1/entities') {
  return render(
    <MemoryRouter initialEntries={[initialEntry]}>
      <Routes>
        <Route path="/team/:teamId/kg/:graphId/:section?" element={<KnowledgeGraphDetail />} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('KnowledgeGraphDetail', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(getKnowledgeGraphDetail).mockResolvedValue({
      kgId: '1',
      teamId: '7',
      name: '支付域图谱',
      myRole: 2,
      enabled: true,
    })
  })

  it('渲染四段菜单', async () => {
    renderShell()
    await screen.findByText('支付域图谱')
    expect(screen.getByText('实体')).toBeInTheDocument()
    expect(screen.getByText('关系')).toBeInTheDocument()
    expect(screen.getByText('模型')).toBeInTheDocument()
    expect(screen.getByText('设置')).toBeInTheDocument()
  })

  it('按 graphId 拉取详情并展示名称', async () => {
    renderShell('/team/7/kg/1/relations')
    expect(await screen.findByText('支付域图谱')).toBeInTheDocument()
    expect(getKnowledgeGraphDetail).toHaveBeenCalledWith(1)
  })

  it('未开启能力时显示提示', async () => {
    vi.mocked(getKnowledgeGraphDetail).mockResolvedValue({
      kgId: '1',
      teamId: '7',
      name: '支付域图谱',
      myRole: 2,
      enabled: false,
    })
    renderShell()
    expect(await screen.findByText(/未开启知识图谱能力/)).toBeInTheDocument()
  })
})

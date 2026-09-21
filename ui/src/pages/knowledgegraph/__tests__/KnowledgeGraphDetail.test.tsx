import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router'
import '@/i18n'
import { KnowledgeGraphDetail } from '../KnowledgeGraphDetail'
import { getKnowledgeGraphCanvas, getKnowledgeGraphDetail } from '@/api/knowledgeGraph'

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
  getKnowledgeGraphCanvas: vi.fn().mockResolvedValue({ nodes: [], edges: [], truncated: false }),
  getKnowledgeGraphNodeNeighbors: vi.fn().mockResolvedValue({ nodes: [], edges: [], truncated: false }),
}))

vi.mock('@/api/kiota', () => ({ getApiClient: vi.fn(() => ({})) }))

// jsdom 无真实 canvas，stub 掉 G6 的渲染管线
vi.mock('@antv/g6', () => {
  const graphStub = {
    setData: vi.fn(),
    render: vi.fn().mockResolvedValue(undefined),
    destroy: vi.fn(),
    on: vi.fn(),
    off: vi.fn(),
  }
  return {
    Graph: function MockGraph() {
      return graphStub
    },
  }
})

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

  it('渲染五段菜单且托管图默认进入图览', async () => {
    renderShell('/team/7/kg/1')
    await screen.findByText('支付域图谱')
    expect(screen.getByText('图览')).toBeInTheDocument()
    expect(screen.getByText('实例')).toBeInTheDocument()
    expect(screen.getByText('关系')).toBeInTheDocument()
    expect(screen.getByText('模型')).toBeInTheDocument()
    expect(screen.getByText('设置')).toBeInTheDocument()
    await waitFor(() => expect(getKnowledgeGraphCanvas).toHaveBeenCalledWith(1, expect.objectContaining({ limit: 200 })))
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

  it('接入图仅显示图览/模型/设置并默认图览、展示只读徽标', async () => {
    vi.mocked(getKnowledgeGraphDetail).mockResolvedValue({
      kgId: '1',
      teamId: '7',
      name: '外部图谱',
      mode: 'connected',
      readOnly: true,
      myRole: 2,
      enabled: true,
    })
    renderShell('/team/7/kg/1')
    expect(await screen.findByText('外部图谱')).toBeInTheDocument()
    expect(await screen.findByText('外部接入 · 只读')).toBeInTheDocument()
    expect(screen.getByText('图览')).toBeInTheDocument()
    expect(screen.getByText('模型')).toBeInTheDocument()
    expect(screen.getByText('设置')).toBeInTheDocument()
    expect(screen.queryByText('实例')).toBeNull()
    expect(screen.queryByText('关系')).toBeNull()
    await waitFor(() => expect(getKnowledgeGraphCanvas).toHaveBeenCalledWith(1, expect.objectContaining({ limit: 200 })))
  })
})

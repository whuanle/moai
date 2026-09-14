import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import '@/i18n'
import { KnowledgeGraphRelations } from '../KnowledgeGraphRelations'
import { useAppStore } from '@/store/app'
import { getKnowledgeGraphEdges, getKnowledgeGraphNodes, getKnowledgeGraphSchema } from '@/api/knowledgeGraph'

vi.mock('@/api/knowledgeGraph', () => ({
  getKnowledgeGraphEdges: vi.fn(),
  getKnowledgeGraphNodes: vi.fn(),
  getKnowledgeGraphSchema: vi.fn(),
  createKnowledgeGraphEdge: vi.fn(),
  updateKnowledgeGraphEdge: vi.fn(),
  deleteKnowledgeGraphEdge: vi.fn(),
}))

vi.mock('@/api/kiota', () => ({ getApiClient: vi.fn(() => ({})) }))

function renderRelations(graphEnabled = true, myRole: number | null = 1) {
  return render(<KnowledgeGraphRelations graphId={1} graphEnabled={graphEnabled} myRole={myRole} />)
}

describe('KnowledgeGraphRelations', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    useAppStore.setState({
      userInfo: { accessToken: 't', userId: '1', userName: 'o' },
      currentTeamId: '7',
      myTeams: [{ teamId: '7', name: 'Alpha', myRole: 2 }],
    })
    vi.mocked(getKnowledgeGraphEdges).mockResolvedValue({
      items: [{ edgeId: 'e1', relationTypeId: 1, sourceNodeId: 'n1', targetNodeId: 'n2' }],
      total: 1,
    })
    vi.mocked(getKnowledgeGraphSchema).mockResolvedValue({
      mode: 'managed',
      entityTypes: [],
      relationTypes: [{ relationTypeId: 1, name: '依赖' }],
    })
    vi.mocked(getKnowledgeGraphNodes).mockResolvedValue({
      items: [
        { nodeId: 'n1', name: '订单服务' },
        { nodeId: 'n2', name: '支付服务' },
      ],
      total: 2,
    })
  })

  it('渲染关系行并映射节点与关系类型名称', async () => {
    renderRelations()
    expect(await screen.findByText('订单服务')).toBeInTheDocument()
    expect(screen.getByText('支付服务')).toBeInTheDocument()
    expect(screen.getByText('依赖')).toBeInTheDocument()
    expect(getKnowledgeGraphEdges).toHaveBeenCalledWith(1, { pageNo: 1, pageSize: 20 })
  })

  it('管理员（能力开启）新建/编辑/删除可用', async () => {
    renderRelations(true, 1)
    expect(await screen.findByRole('button', { name: /新建关系/ })).toBeEnabled()
    expect(await screen.findByRole('button', { name: /^编辑$/ })).toBeEnabled()
    expect(await screen.findByRole('button', { name: /^删除$/ })).toBeEnabled()
  })

  it('能力未开启时不显示新建/编辑/删除入口', async () => {
    renderRelations(false, 1)
    expect(await screen.findByText('依赖')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /新建关系/ })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /^编辑$/ })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /^删除$/ })).not.toBeInTheDocument()
  })

  it('普通成员对关系数据只读（不显示写入口）', async () => {
    renderRelations(true, 0)
    expect(await screen.findByText('依赖')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /新建关系/ })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /^编辑$/ })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /^删除$/ })).not.toBeInTheDocument()
  })
})

import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import '@/i18n'
import { KnowledgeGraphEntities } from '../KnowledgeGraphEntities'
import { getKnowledgeGraphNodes, getKnowledgeGraphSchema } from '@/api/knowledgeGraph'

vi.mock('@/api/knowledgeGraph', () => ({
  getKnowledgeGraphNodes: vi.fn(),
  getKnowledgeGraphSchema: vi.fn(),
  createKnowledgeGraphNode: vi.fn(),
  updateKnowledgeGraphNode: vi.fn(),
  deleteKnowledgeGraphNode: vi.fn(),
}))

vi.mock('@/api/kiota', () => ({ getApiClient: vi.fn(() => ({})) }))

function renderEntities(graphEnabled = true, myRole: number | null = 1) {
  return render(<KnowledgeGraphEntities graphId={1} graphEnabled={graphEnabled} myRole={myRole} />)
}

describe('KnowledgeGraphEntities', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(getKnowledgeGraphNodes).mockResolvedValue({
      items: [{ nodeId: 'n1', entityTypeId: 1, name: '支付服务', description: '核心支付' }],
      total: 1,
    })
    vi.mocked(getKnowledgeGraphSchema).mockResolvedValue({
      entityTypes: [{ entityTypeId: 1, name: '服务' }],
      relationTypes: [],
    })
  })

  it('渲染节点行并映射实体类型名称', async () => {
    renderEntities()
    expect(await screen.findByText('支付服务')).toBeInTheDocument()
    expect(screen.getByText('服务')).toBeInTheDocument()
    expect(getKnowledgeGraphNodes).toHaveBeenCalledWith(1, { keyword: '', pageNo: 1, pageSize: 20 })
  })

  it('无实体类型时「新建实体」禁用', async () => {
    vi.mocked(getKnowledgeGraphSchema).mockResolvedValue({ entityTypes: [], relationTypes: [] })
    renderEntities()
    const button = await screen.findByRole('button', { name: /新建实体/ })
    expect(button).toBeDisabled()
  })

  it('有实体类型时「新建实体」可用', async () => {
    renderEntities()
    const button = await screen.findByRole('button', { name: /新建实体/ })
    expect(button).toBeEnabled()
  })

  it('能力未开启时不显示「新建实体」入口', async () => {
    renderEntities(false, 1)
    expect(await screen.findByText('支付服务')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /新建实体/ })).not.toBeInTheDocument()
  })

  it('普通成员对节点数据只读（不显示写入口）', async () => {
    renderEntities(true, 0)
    expect(await screen.findByText('支付服务')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /新建实体/ })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /^编辑$/ })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /^删除$/ })).not.toBeInTheDocument()
  })
})

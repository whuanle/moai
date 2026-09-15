import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import '@/i18n'
import { KnowledgeGraphSchema } from '../KnowledgeGraphSchema'
import { useAppStore } from '@/store/app'
import { getKnowledgeGraphSchema } from '@/api/knowledgeGraph'

vi.mock('@/api/knowledgeGraph', () => ({
  getKnowledgeGraphSchema: vi.fn(),
  createEntityType: vi.fn(),
  updateEntityType: vi.fn(),
  deleteEntityType: vi.fn(),
  createRelationType: vi.fn(),
  updateRelationType: vi.fn(),
  deleteRelationType: vi.fn(),
}))

vi.mock('@/api/kiota', () => ({ getApiClient: vi.fn(() => ({})) }))

function renderSchema(myRole: number | null, mode?: string | null) {
  return render(<MemoryRouter><KnowledgeGraphSchema graphId={1} teamId={1} myRole={myRole} mode={mode} /></MemoryRouter>)
}

describe('KnowledgeGraphSchema', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    useAppStore.setState({
      userInfo: { accessToken: 't', userId: '1', userName: 'o' },
      currentTeamId: '7',
      myTeams: [{ teamId: '7', name: 'Alpha', myRole: 2 }],
    })
  })

  it('托管模式管理员渲染类型并显示新增入口', async () => {
    vi.mocked(getKnowledgeGraphSchema).mockResolvedValue({
      mode: 'managed',
      entityTypes: [{ entityTypeId: 1, name: '服务', color: '#3366ff' }],
      relationTypes: [{ relationTypeId: 1, name: '依赖', sourceTypeId: null, targetTypeId: null }],
      propertyKeys: [],
    })
    renderSchema(2)
    expect(await screen.findByText('服务')).toBeInTheDocument()
    expect(screen.getByText('依赖')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /新增实体类型/ })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /新增关系类型/ })).toBeInTheDocument()
  })

  it('普通成员不显示新增/编辑/删除入口', async () => {
    vi.mocked(getKnowledgeGraphSchema).mockResolvedValue({
      mode: 'managed',
      entityTypes: [{ entityTypeId: 1, name: '服务' }],
      relationTypes: [],
      propertyKeys: [],
    })
    renderSchema(0)
    expect(await screen.findByText('服务')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /新增实体类型/ })).toBeNull()
    expect(screen.queryByRole('button', { name: /新增关系类型/ })).toBeNull()
    expect(screen.queryByRole('button', { name: /^编辑$/ })).toBeNull()
    expect(screen.queryByRole('button', { name: /^删除$/ })).toBeNull()
  })

  it('接入模式只读展示标签/关系数量与属性键，无增删改', async () => {
    vi.mocked(getKnowledgeGraphSchema).mockResolvedValue({
      mode: 'connected',
      database: 'neo4j',
      readOnly: true,
      entityTypes: [{ name: 'Person', count: 10 }],
      relationTypes: [{ name: 'KNOWS', count: 3 }],
      propertyKeys: ['name', 'age'],
    })
    renderSchema(2)
    expect(await screen.findByText('Person')).toBeInTheDocument()
    expect(screen.getByText('KNOWS')).toBeInTheDocument()
    expect(screen.getByText('name')).toBeInTheDocument()
    expect(screen.getByText('age')).toBeInTheDocument()
    expect(screen.getByText('10')).toBeInTheDocument()
    expect(screen.getByText('3')).toBeInTheDocument()
    expect(screen.getByText('标签（实体类型）')).toBeInTheDocument()
    expect(screen.getByText('关系类型')).toBeInTheDocument()
    expect(screen.getByText('属性键')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /新增实体类型/ })).toBeNull()
    expect(screen.queryByRole('button', { name: /新增关系类型/ })).toBeNull()
    expect(screen.queryByRole('button', { name: /^编辑$/ })).toBeNull()
    expect(screen.queryByRole('button', { name: /^删除$/ })).toBeNull()
  })

  it('详情下传 connected 时即使 schema 拉取失败也只渲染只读视图', async () => {
    vi.mocked(getKnowledgeGraphSchema).mockRejectedValue(new Error('schema unavailable'))
    renderSchema(2, 'connected')
    expect(await screen.findByText('标签（实体类型）')).toBeInTheDocument()
    expect(screen.getByText('属性键')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /新增实体类型/ })).toBeNull()
    expect(screen.queryByRole('button', { name: /新增关系类型/ })).toBeNull()
  })
})

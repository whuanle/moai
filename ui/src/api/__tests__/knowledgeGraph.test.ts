import { describe, expect, it, vi, beforeEach } from 'vitest'

const mocks = vi.hoisted(() => ({
  listGet: vi.fn(),
  createPost: vi.fn(),
  detailGet: vi.fn(),
  deleteFn: vi.fn(),
  schemaGet: vi.fn(),
  nodesListPost: vi.fn(),
  nodesPost: vi.fn(),
  nodePut: vi.fn(),
  nodeDelete: vi.fn(),
}))

vi.mock('@/api/kiota', () => ({
  getApiClient: () => ({
    api: {
      knowledgeGraph: {
        list: { get: mocks.listGet },
        post: mocks.createPost,
        templates: { get: vi.fn().mockResolvedValue({ items: [] }) },
        byId: () => ({
          get: mocks.detailGet,
          delete: mocks.deleteFn,
          schema: { get: mocks.schemaGet },
          nodes: {
            list: { post: mocks.nodesListPost },
            post: mocks.nodesPost,
            byNodeId: () => ({ put: mocks.nodePut, delete: mocks.nodeDelete }),
          },
        }),
      },
    },
  }),
}))

import { getKnowledgeGraphs, createKnowledgeGraph, deleteKnowledgeGraph, getKnowledgeGraphNodes, createKnowledgeGraphNode } from '../knowledgeGraph'

describe('knowledgeGraph api', () => {
  beforeEach(() => vi.clearAllMocks())

  it('getKnowledgeGraphs 传递 teamId 字符串并归一 items', async () => {
    mocks.listGet.mockResolvedValue({ teamId: '7', myRole: 2, enabled: true, items: [{ kgId: '1', name: '域图' }] })
    const res = await getKnowledgeGraphs(7)
    expect(mocks.listGet).toHaveBeenCalledWith({ queryParameters: { teamId: '7' } })
    expect(res.items?.[0].name).toBe('域图')
    expect(res.enabled).toBe(true)
  })

  it('deleteKnowledgeGraph 使用字符串 id', async () => {
    mocks.deleteFn.mockResolvedValue(undefined)
    await deleteKnowledgeGraph(3)
    expect(mocks.deleteFn).toHaveBeenCalled()
  })

  it('getKnowledgeGraphNodes 归一 total', async () => {
    mocks.nodesListPost.mockResolvedValue({ total: '5', items: [] })
    const res = await getKnowledgeGraphNodes(1, { pageNo: 1, pageSize: 20 })
    expect(res.total).toBe(5)
  })
})

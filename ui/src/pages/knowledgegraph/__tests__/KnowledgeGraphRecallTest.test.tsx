import { describe, expect, it, vi, beforeEach } from 'vitest'
import { act, render, screen, fireEvent, waitFor } from '@testing-library/react'
import '@/i18n'
import { KnowledgeGraphRecallTest } from '../KnowledgeGraphRecallTest'
import { recallKnowledgeGraphTest } from '@/api/knowledgeGraph'

vi.mock('@/api/knowledgeGraph', () => ({
  getKnowledgeGraphModelOptions: vi.fn().mockResolvedValue({ conversationModels: [{ id: 'm1', name: '桩模型' }], embeddingModels: [] }),
  recallKnowledgeGraphTest: vi.fn(),
}))

vi.mock('@/api/kiota', () => ({ getApiClient: vi.fn(() => ({})) }))

describe('KnowledgeGraphRecallTest', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('输入问题后发起召回并渲染命中项与一跳邻居', async () => {
    vi.mocked(recallKnowledgeGraphTest).mockResolvedValue({
      query: '运价',
      optimizedQuery: '',
      answer: '',
      hits: [
        {
          kgId: 9, nodeId: 'n-1', name: '上海 → 宁波', description: '航段', entityTypeId: 2, entityTypeName: '航段', score: 0.91,
          neighbors: [{ relationName: '出发', direction: 'out', name: '上海港', description: '' }],
        },
      ],
      skippedHints: [],
    })
    render(<KnowledgeGraphRecallTest kgId={9} teamId={7} />)
    const editor = screen.getByRole('textbox')
    fireEvent.change(editor, { target: { value: '运价' } })
    fireEvent.click(screen.getByRole('button', { name: /开始测试/ }))
    await waitFor(() => expect(recallKnowledgeGraphTest).toHaveBeenCalledWith(9, expect.objectContaining({ query: '运价', top: 5, isOptimizeQuery: false, isAnswer: false })))
    expect(await screen.findByText('召回结果')).toBeInTheDocument()
    expect(screen.getByText('上海 → 宁波')).toBeInTheDocument()
    expect(screen.getAllByText('航段').length).toBeGreaterThan(0)
    expect(screen.getByText(/出发/)).toBeInTheDocument()
  })

  it('开启 AI 回答后未选模型时校验必填且提交携带模型', async () => {
    vi.mocked(recallKnowledgeGraphTest).mockResolvedValue({
      query: '运价', optimizedQuery: '运价 查询', answer: '运价是 800 元。',
      hits: [], skippedHints: [],
    })
    render(<KnowledgeGraphRecallTest kgId={9} teamId={7} />)
    // 等待模型选项 mock 的 promise 落地（自动带模型依赖它）
    await act(async () => { await Promise.resolve() })
    const editor = screen.getByRole('textbox')
    fireEvent.change(editor, { target: { value: '运价' } })
    fireEvent.click(screen.getByRole('switch', { name: 'AI 回答' }))
    // 模型选项 mock 为异步解析，自动带模型后重试点击直到提交成功
    await waitFor(() => {
      fireEvent.click(screen.getByRole('button', { name: /开始测试/ }))
      expect(recallKnowledgeGraphTest).toHaveBeenCalled()
    })
    expect(await screen.findByText('运价是 800 元。')).toBeInTheDocument()
    expect(screen.getByText('由 桩模型 生成')).toBeInTheDocument()
  })

  it('命中为空时渲染空态提示', async () => {
    vi.mocked(recallKnowledgeGraphTest).mockResolvedValue({ query: '不存在', optimizedQuery: '', answer: '', hits: [], skippedHints: [] })
    render(<KnowledgeGraphRecallTest kgId={9} teamId={7} />)
    const editor = screen.getByRole('textbox')
    fireEvent.change(editor, { target: { value: '不存在' } })
    fireEvent.click(screen.getByRole('button', { name: /开始测试/ }))
    expect(await screen.findByText(/没有命中的实体/)).toBeInTheDocument()
  })
})

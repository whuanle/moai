import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { WikiRecallTest } from '../WikiRecallTest'
import { getWikiDocuments, getWikiModelOptions, recallWikiTest } from '@/api/wiki'

vi.mock('@/api/wiki', () => ({
  getWikiDocuments: vi.fn(),
  getWikiModelOptions: vi.fn(),
  recallWikiTest: vi.fn(),
}))

vi.mock('@/api/kiota', () => ({
  getApiClient: vi.fn(() => ({})),
}))

function renderRecall() {
  return render(<WikiRecallTest wikiId={7} teamId={3} />)
}

describe('WikiRecallTest', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(getWikiDocuments).mockResolvedValue({
      items: [
        { documentId: 101, fileName: '产品手册.md' },
        { documentId: 102, fileName: 'FAQ.docx' },
      ],
      total: 2,
      pageNo: 1,
      pageSize: 100,
    } as never)
    vi.mocked(getWikiModelOptions).mockResolvedValue({
      embeddingModels: [],
      conversationModels: [{ id: 'conv-1', name: 'gpt-mini', modelKind: 'conversation' }],
      rerankModels: [],
    })
    vi.mocked(recallWikiTest).mockResolvedValue({
      query: '退货政策',
      optimizedQuery: '',
      answer: '',
      items: [
        {
          documentId: 101,
          documentName: '产品手册.md',
          chunkId: '9001',
          metadataType: 0,
          content: '商品签收后 7 天内可无理由退货。',
          score: 0.8734,
        },
      ],
    })
  })

  it('渲染查询表单并加载文档范围与对话模型选项', async () => {
    renderRecall()

    expect(screen.getByText('查询问题')).toBeInTheDocument()
    expect(screen.getByText('文档范围')).toBeInTheDocument()
    expect(screen.getByText('相似度阈值')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /搜\s*索召回/ })).toBeInTheDocument()

    await waitFor(() => {
      expect(getWikiDocuments).toHaveBeenCalledWith(7, { pageNo: 1, pageSize: 100 })
      expect(getWikiModelOptions).toHaveBeenCalledWith(3)
    })
  })

  it('未开启 AI 开关时模型选择器禁用', async () => {
    renderRecall()

    await waitFor(() => {
      expect(getWikiModelOptions).toHaveBeenCalled()
    })
    expect(screen.getByLabelText('对话模型')).toBeDisabled()
    expect(screen.getByLabelText('AI 优化问题')).not.toBeChecked()
  })

  it('开启 AI 优化问题后未选模型时不提交', async () => {
    vi.mocked(getWikiModelOptions).mockResolvedValue({
      embeddingModels: [],
      conversationModels: [],
      rerankModels: [],
    })
    renderRecall()

    const queryInput = screen.getByLabelText('查询问题')
    fireEvent.change(queryInput, { target: { value: '退货政策' } })
    fireEvent.click(screen.getByLabelText('AI 优化问题'))
    fireEvent.click(screen.getByRole('button', { name: /搜\s*索召回/ }))

    await waitFor(() => {
      expect(screen.getByText('开启 AI 优化或回答时请选择模型')).toBeInTheDocument()
    })
    expect(recallWikiTest).not.toHaveBeenCalled()
  })

  it('提交召回请求并渲染命中结果', async () => {
    renderRecall()

    const queryInput = await screen.findByLabelText('查询问题')
    fireEvent.change(queryInput, { target: { value: '退货政策' } })
    fireEvent.click(screen.getByRole('button', { name: /搜\s*索召回/ }))

    await waitFor(() => {
      expect(recallWikiTest).toHaveBeenCalledWith(7, {
        query: '退货政策',
        documentIds: [],
        top: 5,
        minScore: null,
        aiModelId: null,
        isOptimizeQuery: false,
        isAnswer: false,
      })
    })
    expect(await screen.findByText('召回结果')).toBeInTheDocument()
    expect(screen.getByText('产品手册.md')).toBeInTheDocument()
    expect(screen.getByText('商品签收后 7 天内可无理由退货。')).toBeInTheDocument()
    expect(screen.getByText(/0\.8734/)).toBeInTheDocument()
  })

  it('返回 AI 优化后的问题与回答时展示对应区块', async () => {
    vi.mocked(recallWikiTest).mockResolvedValue({
      query: '退货政策是什么',
      optimizedQuery: '退货政策',
      answer: '签收后 7 天内可无理由退货。',
      items: [],
    })
    renderRecall()

    const queryInput = await screen.findByLabelText('查询问题')
    fireEvent.change(queryInput, { target: { value: '退货政策是什么' } })
    fireEvent.click(screen.getByRole('button', { name: /搜\s*索召回/ }))

    expect(await screen.findByText('优化后的问题：退货政策')).toBeInTheDocument()
    expect(screen.getByText('AI 回答')).toBeInTheDocument()
    expect(screen.getByText('签收后 7 天内可无理由退货。')).toBeInTheDocument()
    expect(screen.getByText('没有符合条件的召回结果')).toBeInTheDocument()
  })

  it('查询内容为空时不提交请求', async () => {
    renderRecall()

    fireEvent.click(await screen.findByRole('button', { name: /搜\s*索召回/ }))

    await waitFor(() => {
      expect(screen.getByText('请输入查询文本')).toBeInTheDocument()
    })
    expect(recallWikiTest).not.toHaveBeenCalled()
  })
})

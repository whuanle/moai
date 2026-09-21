import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { BatchWorkflowModal } from '../BatchWorkflowModal'
import { batchRunWikiDocumentsWorkflow, getWikiDetail, getWikiModelOptions } from '@/api/wiki'

vi.mock('@/api/wiki', () => ({
  getWikiDetail: vi.fn(),
  getWikiModelOptions: vi.fn(),
  batchRunWikiDocumentsWorkflow: vi.fn(),
}))

vi.mock('@/api/kiota', () => ({
  getApiClient: vi.fn(() => ({})),
}))

function renderModal(props: Partial<Parameters<typeof BatchWorkflowModal>[0]> = {}) {
  const onClose = vi.fn()
  const onDone = vi.fn()
  render(
    <BatchWorkflowModal
      wikiId={7}
      teamId={3}
      open
      documentIds={[101, 102]}
      onClose={onClose}
      onDone={onDone}
      {...props}
    />,
  )
  return { onClose, onDone }
}

describe('BatchWorkflowModal', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(getWikiDetail).mockResolvedValue({
      wikiId: '7',
      teamId: '3',
      name: 'wk',
      embeddingModelId: 'emb-1',
      embeddingDimensions: 1024,
    } as never)
    vi.mocked(getWikiModelOptions).mockResolvedValue({
      embeddingModels: [],
      conversationModels: [{ id: 'conv-1', name: 'gpt-mini', modelKind: 'conversation' }],
      rerankModels: [],
    } as never)
    vi.mocked(batchRunWikiDocumentsWorkflow).mockResolvedValue([
      { documentId: 101, fileName: 'a.md', success: true, message: '已提交元数据生成与向量化任务' },
      { documentId: 102, fileName: 'b.md', success: false, message: '切割失败：解析异常' },
    ])
  })

  it('打开时按通用默认值预填步骤（切割 + 向量化）', async () => {
    renderModal()
    await waitFor(() => expect(getWikiDetail).toHaveBeenCalledWith(7))
    // 默认勾选切割与向量化两步（生成元数据默认不勾）
    await waitFor(() => {
      const checkboxes = screen.getAllByRole('checkbox')
      expect(checkboxes.length).toBeGreaterThanOrEqual(3)
    })
  })

  it('只勾选切割一步时提交负载仅包含切割步骤', async () => {
    const { onDone } = renderModal()
    await waitFor(() => expect(screen.getByText('批量处理文档')).toBeInTheDocument())
    // 默认勾选切割 + 向量化，取消向量化仅保留切割（生成元数据默认未勾选）
    fireEvent.click(screen.getByRole('checkbox', { name: '向量化' }))
    await waitFor(() => expect(screen.queryByText('生成元数据')).not.toBeNull())

    const submit = screen.getByRole('button', { name: /开始处理（2）/ })
    fireEvent.click(submit)
    await waitFor(() => expect(batchRunWikiDocumentsWorkflow).toHaveBeenCalled())
    const payload = vi.mocked(batchRunWikiDocumentsWorkflow).mock.calls[0][1]
    expect(payload.isPartition).toBe(true)
    expect(payload.isGenerateMetadata).toBe(false)
    expect(payload.isEmbedding).toBe(false)
    expect(payload.chunkSize).toBe(1000)
    await waitFor(() => expect(onDone).toHaveBeenCalled())
    // 结果列表展示逐文档成功/失败
    await waitFor(() => expect(screen.getByText('a.md')).toBeInTheDocument())
    expect(screen.getByText('切割失败：解析异常')).toBeInTheDocument()
  })

  it('直接提交时负载为默认三步组合（切割 + 向量化，无元数据策略）', async () => {
    const { onDone } = renderModal()
    await waitFor(() => expect(screen.getByText('批量处理文档')).toBeInTheDocument())
    const submit = screen.getByRole('button', { name: /开始处理（2）/ })
    fireEvent.click(submit)
    await waitFor(() => expect(batchRunWikiDocumentsWorkflow).toHaveBeenCalled())
    const payload = vi.mocked(batchRunWikiDocumentsWorkflow).mock.calls[0][1]
    expect(payload.isPartition).toBe(true)
    expect(payload.isAiPartition).toBe(false)
    expect(payload.isGenerateMetadata).toBe(false)
    expect(payload.strategyTypes).toBeNull()
    expect(payload.isEmbedding).toBe(true)
    expect(payload.embedSourceText).toBe(true)
    expect(payload.embedMetadata).toBe(true)
    await waitFor(() => expect(onDone).toHaveBeenCalled())
  })

  it('向量化模型未配置时禁用提交', async () => {
    vi.mocked(getWikiDetail).mockResolvedValue({
      wikiId: '7',
      teamId: '3',
      name: 'wk',
      embeddingModelId: null,
      embeddingDimensions: 0,
    } as never)
    renderModal()
    await waitFor(() => expect(screen.getByRole('button', { name: /开始处理（2）/ })).toBeDisabled())
  })
})

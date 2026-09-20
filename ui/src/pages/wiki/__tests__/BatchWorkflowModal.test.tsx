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
      workflowConfig: {
        partition: { mode: 'normal', splitMode: 'recursive', chunkSize: 512, chunkOverlap: 30, overlapUnit: 'character', sizeUnit: 'character' },
        metadata: { metadataModelId: 'conv-1', strategyTypes: ['outlineGeneration', 'questionGeneration'] },
        embedding: { embedSourceText: true, embedMetadata: false },
      },
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

  it('打开时按知识库默认工作流预填步骤', async () => {
    renderModal()
    await waitFor(() => expect(getWikiDetail).toHaveBeenCalledWith(7))
    // 三个步骤都按默认工作流勾选
    await waitFor(() => {
      const checkboxes = screen.getAllByRole('checkbox')
      expect(checkboxes.length).toBeGreaterThanOrEqual(3)
    })
  })

  it('只勾选切割一步时提交负载仅包含切割步骤', async () => {
    const { onDone } = renderModal()
    await waitFor(() => expect(screen.getByText('批量处理文档')).toBeInTheDocument())
    // 取消元数据与向量化，仅保留切割
    const checkboxes = screen.getAllByRole('checkbox')
    // 表单初始渲染包含 partition/metadata/embedding 三个步骤 checkbox（embedding 子项在勾选时才出现）
    fireEvent.click(checkboxes[1])
    fireEvent.click(checkboxes[2])
    await waitFor(() => expect(screen.queryByText('生成元数据')).not.toBeNull())

    const submit = screen.getByRole('button', { name: /开始处理（2）/ })
    fireEvent.click(submit)
    await waitFor(() => expect(batchRunWikiDocumentsWorkflow).toHaveBeenCalled())
    const payload = vi.mocked(batchRunWikiDocumentsWorkflow).mock.calls[0][1]
    expect(payload.isPartition).toBe(true)
    expect(payload.isGenerateMetadata).toBe(false)
    expect(payload.isEmbedding).toBe(false)
    expect(payload.chunkSize).toBe(512)
    await waitFor(() => expect(onDone).toHaveBeenCalled())
    // 结果列表展示逐文档成功/失败
    await waitFor(() => expect(screen.getByText('a.md')).toBeInTheDocument())
    expect(screen.getByText('切割失败：解析异常')).toBeInTheDocument()
  })

  it('默认工作流含多选策略时提交负载携带策略数组', async () => {
    const { onDone } = renderModal()
    await waitFor(() => expect(screen.getByText('批量处理文档')).toBeInTheDocument())
    const submit = screen.getByRole('button', { name: /开始处理（2）/ })
    fireEvent.click(submit)
    await waitFor(() => expect(batchRunWikiDocumentsWorkflow).toHaveBeenCalled())
    const payload = vi.mocked(batchRunWikiDocumentsWorkflow).mock.calls[0][1]
    expect(payload.strategyTypes).toEqual(['outlineGeneration', 'questionGeneration'])
    expect(payload.isPartition).toBe(true)
    expect(payload.isAiPartition).toBe(false)
    await waitFor(() => expect(onDone).toHaveBeenCalled())
  })

  it('向量化模型未配置时禁用提交', async () => {
    vi.mocked(getWikiDetail).mockResolvedValue({
      wikiId: '7',
      teamId: '3',
      name: 'wk',
      embeddingModelId: null,
      embeddingDimensions: 0,
      workflowConfig: null,
    } as never)
    renderModal()
    await waitFor(() => expect(screen.getByRole('button', { name: /开始处理（2）/ })).toBeDisabled())
  })
})

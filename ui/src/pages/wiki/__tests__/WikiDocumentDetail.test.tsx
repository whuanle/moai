import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router'
import { WikiDocumentDetail } from '../WikiDocumentDetail'
import {
  aiPartitionWikiDocument,
  extractWikiDocumentContent,
  generateWikiDocumentChunkMetadata,
  generateWikiDocumentChunksMetadata,
  getWikiDocumentContent,
  getWikiDocumentEmbedding,
  partitionWikiDocument,
  triggerDocumentEmbedding,
} from '@/api/wiki'
import { feedback } from '@/design-system'
import { aichannelApi } from '@/api/aichannel'

import '@/i18n'

vi.mock('@/api/wiki', () => ({
  getWikiDocumentEmbedding: vi.fn(),
  triggerDocumentEmbedding: vi.fn().mockResolvedValue(undefined),
  extractWikiDocumentContent: vi.fn().mockResolvedValue(undefined),
  getWikiDocumentContent: vi.fn().mockResolvedValue(''),
  partitionWikiDocument: vi.fn().mockResolvedValue(undefined),
  aiPartitionWikiDocument: vi.fn().mockResolvedValue(undefined),
  generateWikiDocumentChunkMetadata: vi.fn().mockResolvedValue(1),
  generateWikiDocumentChunksMetadata: vi.fn().mockResolvedValue(1),
}))

vi.mock('@/api/kiota', () => ({
  getApiClient: vi.fn(() => ({})),
}))

vi.mock('@/api/aichannel', () => ({
  aichannelApi: {
    getModels: vi.fn().mockResolvedValue([
      { id: 'conv-id', name: 'gpt-4.1-mini', modelKind: 'conversation' },
      { id: 'emb-id', name: 'text-embedding-3-large', modelKind: 'embedding' },
    ]),
  },
}))

function renderDetail(initialPath = '/team/3/wiki/7/document/100/embedding') {
  return render(
    <MemoryRouter initialEntries={[initialPath]}>
      <Routes>
        <Route path="/team/:teamId/wiki/:wikiId/document/:documentId/:section?" element={<WikiDocumentDetail />} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('WikiDocumentDetail', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(getWikiDocumentEmbedding).mockResolvedValue({
      wikiId: 7,
      wikiName: '产品文档',
      embeddingModelId: 'emb-id',
      embeddingModelName: 'text-embedding-3-large',
      embeddingDimensions: 1024,
      isLock: false,
      chunkSize: 512,
      chunkOverlap: 64,
      documentId: 100,
      fileName: 'demo.md',
      isEmbedding: false,
      embeddingCount: 0,
      isContentExtracted: true,
      contentLength: 42,
      // 内容较短时 detail.content 即全文（预览长度=全文长度 → 不触发「加载完整内容」）
      content: '这是从 demo.md 自动提取的完整文件内容，用于文档内容区默认展示。中文标点符号。',
      items: [
        { chunkId: 1, sliceOrder: 1, sliceContent: '知识库向量化测试内容片段', metadataCount: 1, metadatas: [{ metadataType: 1, metadataContent: '大纲内容' }] },
        { chunkId: 2, sliceOrder: 2, sliceContent: '第二段切片内容', metadataCount: 0, metadatas: [] },
      ],
    } as never)
  })

  it('渲染文档操作页：文件、内容提取状态、切片预览', async () => {
    renderDetail()

    expect(await screen.findByText('demo.md')).toBeInTheDocument()
    expect(screen.getByText('内容已提取（42 字符）')).toBeInTheDocument()
    expect(screen.getByText('知识库向量化测试内容片段')).toBeInTheDocument()
    expect(screen.getByText('第二段切片内容')).toBeInTheDocument()
    // 上传已自动提取内容 → 按钮退化为「重新提取」（兜底重试），文件名与状态在卡片右上角
    expect(screen.getByRole('button', { name: /重新提取/ })).toBeInTheDocument()
    expect(screen.queryByText('文档上传后已自动提取内容，可直接进行切割')).not.toBeInTheDocument()
    // 内容默认不在页面内展开，提供「查看内容」入口（模态窗查看）
    expect(screen.getByRole('button', { name: /查看内容/ })).toBeInTheDocument()
    expect(getWikiDocumentEmbedding).toHaveBeenCalledWith(7, 100)
    expect(aichannelApi.getModels).toHaveBeenCalledWith(undefined, 3)
  })

  it('未提取内容时展示提示，且不渲染向量化表单', async () => {
    vi.mocked(getWikiDocumentEmbedding).mockResolvedValue({
      wikiId: 7,
      wikiName: '产品文档',
      embeddingModelId: 'emb-id',
      embeddingModelName: 'text-embedding-3-large',
      embeddingDimensions: 1024,
      isLock: false,
      chunkSize: 0,
      chunkOverlap: 0,
      documentId: 100,
      fileName: 'demo.md',
      isEmbedding: false,
      embeddingCount: 0,
      isContentExtracted: false,
      contentLength: 0,
      items: [],
    } as never)
    renderDetail()

    expect(await screen.findByText('内容未提取')).toBeInTheDocument()
    const tips = await screen.findAllByText('文档内容尚未就绪，无法切割')
    expect(tips.length).toBeGreaterThan(0)
    expect(screen.queryByRole('button', { name: /开始向量化/ })).not.toBeInTheDocument()
  })

  it('点击提取内容调用 extractWikiDocumentContent（@WK-S19，兜底重试）', async () => {
    vi.mocked(getWikiDocumentEmbedding).mockResolvedValue({
      wikiId: 7,
      wikiName: '产品文档',
      embeddingModelId: 'emb-id',
      embeddingModelName: 'text-embedding-3-large',
      embeddingDimensions: 1024,
      isLock: false,
      chunkSize: 0,
      chunkOverlap: 0,
      documentId: 100,
      fileName: 'demo.md',
      isEmbedding: false,
      embeddingCount: 0,
      isContentExtracted: false,
      contentLength: 0,
      items: [],
    } as never)
    renderDetail()

    fireEvent.click(await screen.findByRole('button', { name: /提取内容/ }))

    await waitFor(() => {
      expect(extractWikiDocumentContent).toHaveBeenCalledWith(7, 100)
    })
  })

  it('普通切割提交 Maomi.ToMarkdown 切割参数', async () => {
    renderDetail()

    expect(await screen.findByLabelText('切割方式')).toBeInTheDocument()
    expect(screen.getByLabelText('长度单位')).toBeInTheDocument()
    expect(screen.getByLabelText('重叠单位')).toBeInTheDocument()
    const chunkSizeInput = await screen.findByLabelText('切片长度（字符）')
    const chunkOverlapInput = await screen.getByLabelText('切片重叠（字符）')
    fireEvent.change(chunkSizeInput, { target: { value: '512' } })
    fireEvent.change(chunkOverlapInput, { target: { value: '64' } })

    fireEvent.click(screen.getByRole('button', { name: /开始切割/ }))

    await waitFor(() => {
      expect(partitionWikiDocument).toHaveBeenCalledWith(7, 100, {
        splitMode: 'markdown',
        chunkSize: 512,
        chunkOverlap: 64,
        overlapUnit: 'character',
        sizeUnit: 'character',
        tokenEncodingOrModel: undefined,
      })
    })
  })

  it('无历史切割配置时默认使用向量维度推荐值', async () => {
    vi.mocked(getWikiDocumentEmbedding).mockResolvedValue({
      wikiId: 7,
      wikiName: '产品文档',
      embeddingModelId: 'emb-id',
      embeddingModelName: 'text-embedding-3-large',
      embeddingDimensions: 1024,
      isLock: false,
      chunkSize: 0,
      chunkOverlap: 0,
      documentId: 100,
      fileName: 'demo.md',
      isEmbedding: false,
      embeddingCount: 0,
      isContentExtracted: true,
      contentLength: 10,
      content: '测试内容',
      items: [{ chunkId: 1, sliceOrder: 1, sliceContent: '测试内容', metadataCount: 0, metadatas: [] }],
    } as never)
    renderDetail()

    expect(await screen.findByText('当前向量维度 1024，推荐切片长度 500-900 Token，默认建议 700 Token；重叠建议保留 1 个句子。')).toBeInTheDocument()
    expect(screen.getByLabelText('切片长度（Token）')).toHaveValue('700')
    expect(screen.getByLabelText('切片重叠（句子）')).toHaveValue('1')

    fireEvent.click(screen.getByRole('button', { name: /开始切割/ }))

    await waitFor(() => {
      expect(partitionWikiDocument).toHaveBeenCalledWith(7, 100, {
        splitMode: 'markdown',
        chunkSize: 700,
        chunkOverlap: 1,
        overlapUnit: 'sentence',
        sizeUnit: 'token',
        tokenEncodingOrModel: 'cl100k_base',
      })
    })
  })

  it('普通切割支持选择 Token 计量与非默认切割模式', async () => {
    renderDetail()

    fireEvent.mouseDown(await screen.findByLabelText('切割方式'))
    fireEvent.click(await screen.findByTitle('递归切割'))
    fireEvent.mouseDown(screen.getByLabelText('长度单位'))
    fireEvent.click(await screen.findByTitle('Token'))
    fireEvent.mouseDown(screen.getByLabelText('重叠单位'))
    fireEvent.click(await screen.findByTitle('句子'))

    fireEvent.change(await screen.findByLabelText('切片长度（Token）'), { target: { value: '256' } })
    fireEvent.change(screen.getByLabelText('切片重叠（句子）'), { target: { value: '1' } })
    fireEvent.mouseDown(await screen.findByLabelText('Token 编码'))
    fireEvent.click(await screen.findByTitle('o200k_base'))

    fireEvent.click(screen.getByRole('button', { name: /开始切割/ }))

    await waitFor(() => {
      expect(partitionWikiDocument).toHaveBeenCalledWith(7, 100, {
        splitMode: 'recursive',
        chunkSize: 256,
        chunkOverlap: 1,
        overlapUnit: 'sentence',
        sizeUnit: 'token',
        tokenEncodingOrModel: 'o200k_base',
      })
    })
  })

  it('句子优先和段落优先会联动重叠单位', async () => {
    renderDetail()

    fireEvent.mouseDown(await screen.findByLabelText('切割方式'))
    fireEvent.click(await screen.findByTitle('句子优先'))
    expect(await screen.findByLabelText('切片重叠（句子）')).toBeInTheDocument()

    fireEvent.mouseDown(screen.getByLabelText('切割方式'))
    fireEvent.click(await screen.findByTitle('段落优先'))
    expect(await screen.findByLabelText('切片重叠（段落）')).toBeInTheDocument()
  })

  it('AI 切割提交 aiModelId', async () => {
    renderDetail()

    fireEvent.click(await screen.findByRole('tab', { name: /AI 切割/ }))
    const modelSelect = await screen.findByLabelText('AI 模型')
    fireEvent.mouseDown(modelSelect)
    fireEvent.click(await screen.findByTitle('gpt-4.1-mini'))

    fireEvent.click(screen.getByRole('button', { name: /智能切割/ }))

    await waitFor(() => {
      expect(aiPartitionWikiDocument).toHaveBeenCalledWith(7, 100, { aiModelId: 'conv-id', promptTemplate: expect.stringContaining('JSON 字符串数组') })
    })
  })

  it('AI 切割失败时展示错误反馈', async () => {
    const error = new Error('AI 切割失败')
    const handleError = vi.spyOn(feedback, 'handleError').mockImplementation(() => undefined)
    vi.mocked(aiPartitionWikiDocument).mockRejectedValueOnce(error)
    renderDetail()

    fireEvent.click(await screen.findByRole('tab', { name: /AI 切割/ }))
    fireEvent.mouseDown(await screen.findByLabelText('AI 模型'))
    fireEvent.click(await screen.findByTitle('gpt-4.1-mini'))
    fireEvent.click(screen.getByRole('button', { name: /智能切割/ }))

    await waitFor(() => {
      expect(handleError).toHaveBeenCalledWith(error)
    })
    handleError.mockRestore()
  })

  it('向量化只提交原文/元数据开关，不再展示元数据生成模型选择器', async () => {
    renderDetail()

    expect(await screen.findByRole('checkbox', { name: '对原文切片向量化' })).toBeInTheDocument()
    expect(screen.getByRole('checkbox', { name: '对元数据向量化' })).toBeInTheDocument()
    expect(screen.queryByLabelText('元数据生成模型（可选）')).not.toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: /开始向量化/ }))

    await waitFor(() => {
      expect(triggerDocumentEmbedding).toHaveBeenCalledWith(7, 100, {
        isEmbedSourceText: true,
        isEmbedMetadata: true,
      })
    })
  })

  it('两项向量化开关都关闭时不提交', async () => {
    const warning = vi.spyOn(feedback, 'warning').mockImplementation(() => undefined)
    renderDetail()

    fireEvent.click(await screen.findByRole('checkbox', { name: '对原文切片向量化' }))
    fireEvent.click(screen.getByRole('checkbox', { name: '对元数据向量化' }))
    fireEvent.click(screen.getByRole('button', { name: /开始向量化/ }))

    await waitFor(() => {
      expect(warning).toHaveBeenCalledWith('至少需要选择一种向量化内容（原文或元数据）')
    })
    expect(triggerDocumentEmbedding).not.toHaveBeenCalled()
    warning.mockRestore()
  })

  it('切片预览支持单个切片生成元数据', async () => {
    renderDetail()

    fireEvent.mouseDown(await screen.findByText('选择生成元数据模型'))
    fireEvent.click(await screen.findByTitle('gpt-4.1-mini'))
    fireEvent.click(screen.getByRole('button', { name: /重新生成元数据/ }))
    fireEvent.click(await screen.findByRole('button', { name: /继续生成元数据/ }))

    await waitFor(() => {
      expect(generateWikiDocumentChunkMetadata).toHaveBeenCalledWith(7, 100, '1', 'conv-id', true, 'outlineGeneration')
    })
  })

  it('生成元数据后保持弹窗打开并刷新基础元数据列表', async () => {
    vi.mocked(getWikiDocumentEmbedding)
      .mockResolvedValueOnce({
        wikiId: 7,
        wikiName: '产品文档',
        embeddingModelId: 'emb-id',
        embeddingModelName: 'text-embedding-3-large',
        embeddingDimensions: 1024,
        isLock: false,
        chunkSize: 512,
        chunkOverlap: 64,
        documentId: 100,
        fileName: 'demo.md',
        isEmbedding: false,
        embeddingCount: 0,
        isContentExtracted: true,
        contentLength: 42,
        content: '这是从 demo.md 自动提取的完整文件内容，用于文档内容区默认展示。中文标点符号。',
        items: [{ chunkId: 2, sliceOrder: 2, sliceContent: '第二段切片内容', metadataCount: 0, metadatas: [] }],
      } as never)
      .mockResolvedValueOnce({
        wikiId: 7,
        wikiName: '产品文档',
        embeddingModelId: 'emb-id',
        embeddingModelName: 'text-embedding-3-large',
        embeddingDimensions: 1024,
        isLock: false,
        chunkSize: 512,
        chunkOverlap: 64,
        documentId: 100,
        fileName: 'demo.md',
        isEmbedding: false,
        embeddingCount: 0,
        isContentExtracted: true,
        contentLength: 42,
        content: '这是从 demo.md 自动提取的完整文件内容，用于文档内容区默认展示。中文标点符号。',
        items: [{ chunkId: 2, sliceOrder: 2, sliceContent: '第二段切片内容', metadataCount: 1, metadatas: [{ metadataType: 1, metadataContent: '新生成的大纲内容' }] }],
      } as never)
    renderDetail()

    fireEvent.mouseDown(await screen.findByText('选择生成元数据模型'))
    fireEvent.click(await screen.findByTitle('gpt-4.1-mini'))
    const openButtons = await screen.findAllByRole('button', { name: /生成元数据/ })
    fireEvent.click(openButtons[openButtons.length - 1])
    const modalGenerateButtons = await screen.findAllByRole('button', { name: /生成元数据/ })
    fireEvent.click(modalGenerateButtons[modalGenerateButtons.length - 1])

    expect(await screen.findByText('新生成的大纲内容')).toBeInTheDocument()
    expect(screen.getByText('生成元数据 - #2')).toBeInTheDocument()
    expect(screen.queryByText('文档向量化等相关能力建设中，敬请期待')).not.toBeInTheDocument()
  })

  it('每个切片可以独立打开元数据预览', async () => {
    renderDetail()

    const previewButtons = await screen.findAllByRole('button', { name: '元数据' })
    fireEvent.click(previewButtons[1])

    expect((await screen.findAllByText('第二段切片内容')).length).toBeGreaterThanOrEqual(2)
    expect(screen.getByText('该切割块暂无元数据')).toBeInTheDocument()
    expect(screen.queryByText('大纲内容')).not.toBeInTheDocument()
  })

  it('切片卡片列表和元数据列表独立滚动', async () => {
    renderDetail()

    const chunkList = await screen.findByTestId('wiki-chunk-card-list')
    expect(chunkList).toHaveStyle({ overflowY: 'auto' })
    expect(chunkList).toHaveStyle({ overflowX: 'hidden' })

    const previewButtons = await screen.findAllByRole('button', { name: '元数据' })
    fireEvent.click(previewButtons[0])

    const metadataList = await screen.findByTestId('wiki-metadata-list')
    expect(metadataList).toHaveStyle({ overflowY: 'auto' })
    expect(metadataList).toHaveStyle({ overflowX: 'hidden' })
  })

  it('未选择模型时点击生成元数据会提示用户', async () => {
    const warning = vi.spyOn(feedback, 'warning').mockImplementation(() => undefined)
    renderDetail()

    fireEvent.click(await screen.findByRole('button', { name: /重新生成元数据/ }))
    const generateButtons = await screen.findAllByRole('button', { name: /生成元数据/ })
    fireEvent.click(generateButtons[generateButtons.length - 1])

    expect(warning).toHaveBeenCalledWith('请先选择生成元数据模型')
    expect(generateWikiDocumentChunkMetadata).not.toHaveBeenCalled()
    warning.mockRestore()
  })

  it('模型未返回有效元数据时不提示生成成功', async () => {
    const warning = vi.spyOn(feedback, 'warning').mockImplementation(() => undefined)
    const success = vi.spyOn(feedback, 'success').mockImplementation(() => undefined)
    vi.mocked(generateWikiDocumentChunkMetadata).mockResolvedValueOnce(0)
    renderDetail()

    fireEvent.mouseDown(await screen.findByText('选择生成元数据模型'))
    fireEvent.click(await screen.findByTitle('gpt-4.1-mini'))
    fireEvent.click(screen.getByRole('button', { name: /重新生成元数据/ }))
    fireEvent.click(await screen.findByRole('button', { name: /继续生成元数据/ }))

    await waitFor(() => {
      expect(warning).toHaveBeenCalledWith('模型未返回有效元数据，请更换策略或模型后重试')
    })
    expect(success).not.toHaveBeenCalledWith('元数据生成成功')
    warning.mockRestore()
    success.mockRestore()
  })

  it('切片预览支持全部切片生成元数据', async () => {
    renderDetail()

    fireEvent.mouseDown(await screen.findByText('选择生成元数据模型'))
    fireEvent.click(await screen.findByTitle('gpt-4.1-mini'))
    fireEvent.click(screen.getByRole('button', { name: /全部生成元数据/ }))

    await waitFor(() => {
      expect(generateWikiDocumentChunksMetadata).toHaveBeenCalledWith(7, 100, 'conv-id', undefined, 'outlineGeneration')
    })
  })

  it('未配置 embedding 模型时展示模型选项加载失败提示', async () => {
    vi.mocked(getWikiDocumentEmbedding).mockResolvedValue({
      wikiId: 7,
      wikiName: '产品文档',
      embeddingModelId: '',
      embeddingModelName: '',
      embeddingDimensions: 0,
      isLock: false,
      chunkSize: 0,
      chunkOverlap: 0,
      documentId: 100,
      fileName: 'demo.md',
      isEmbedding: false,
      embeddingCount: 0,
      isContentExtracted: true,
      contentLength: 10,
      items: [{ chunkId: 1, sliceOrder: 1, sliceContent: 'x', metadataCount: 0, metadatas: [] }],
    } as never)
    renderDetail()

    expect(await screen.findByText('模型选项加载失败，请稍后重试')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /开始向量化/ })).not.toBeInTheDocument()
  })

  it('API 调用失败（后端 404，非成员）时展示 operationPlaceholder', async () => {
    vi.mocked(getWikiDocumentEmbedding).mockRejectedValueOnce(new Error('404'))
    renderDetail()

    expect(await screen.findByText('文档向量化等相关能力建设中，敬请期待')).toBeInTheDocument()
  })

  it('点击「查看内容」在模态窗中展示文档内容（@WK-S19）', async () => {
    renderDetail()

    fireEvent.click(await screen.findByRole('button', { name: /查看内容/ }))

    // 内容未被截断时直接展示，无需调用全文接口（页面摘要行 + 模态窗各一份）
    expect((await screen.findAllByText(/这是从 demo.md 自动提取的完整文件内容/)).length).toBeGreaterThan(0)
    expect(getWikiDocumentContent).not.toHaveBeenCalled()
    // Modal 首次打开带入场动画，footer 按钮需等待渲染（antd 会在两个中文字符间插入空格）
    expect(await screen.findByRole('button', { name: /关\s*闭/ })).toBeInTheDocument()
  })

  it('预览被截断时打开模态窗自动加载完整内容（@WK-S19）', async () => {
    const preview = '这是很长文档的前段预览，'.repeat(3) // detail.content = 截断预览
    vi.mocked(getWikiDocumentEmbedding).mockResolvedValue({
      wikiId: 7,
      wikiName: '产品文档',
      embeddingModelId: 'emb-id',
      embeddingModelName: 'text-embedding-3-large',
      embeddingDimensions: 1024,
      isLock: false,
      chunkSize: 0,
      chunkOverlap: 0,
      documentId: 100,
      fileName: 'huge.md',
      isEmbedding: false,
      embeddingCount: 0,
      isContentExtracted: true,
      contentLength: 20000, // 全文 2 万字，远超预览
      content: preview,
      contentPreviewLength: preview.length,
      items: [],
    } as never)
    const full = '这是完整内容'.repeat(400) // 全文示例
    vi.mocked(getWikiDocumentContent).mockResolvedValueOnce(full)
    renderDetail()

    fireEvent.click(await screen.findByRole('button', { name: /查看内容/ }))

    await waitFor(() => {
      expect(getWikiDocumentContent).toHaveBeenCalledWith(7, 100)
    })
    expect((await screen.findAllByText(/这是完整内容/)).length).toBeGreaterThan(0)
  })
})

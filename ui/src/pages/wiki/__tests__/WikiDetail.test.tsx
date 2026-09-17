import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router'
import { WikiDetail } from '../WikiDetail'
import { getWikiDetail, updateWiki, getWikiDocuments, getWikiModelOptions, updateWikiEmbeddingConfig, updateWikiRerankModel } from '@/api/wiki'

vi.mock('@/api/wiki', () => ({
  getWikiDetail: vi.fn(),
  updateWiki: vi.fn().mockResolvedValue(undefined),
  getWikiDocuments: vi.fn(),
  getWikiModelOptions: vi.fn(),
  updateWikiEmbeddingConfig: vi.fn().mockResolvedValue(undefined),
  updateWikiRerankModel: vi.fn().mockResolvedValue(undefined),
  getWikiUploadLimit: vi.fn().mockResolvedValue(0),
}))

vi.mock('@/api/kiota', () => ({
  getApiClient: vi.fn(() => ({})),
}))

function renderDetail(initialPath = '/team/3/wiki/7') {
  return render(
    <MemoryRouter initialEntries={[initialPath]}>
      <Routes>
        <Route path="/team/:teamId/wiki/:wikiId/:section?" element={<WikiDetail />} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('WikiDetail', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(getWikiDetail).mockResolvedValue({
      wikiId: '7',
      teamId: '3',
      name: '产品文档',
      description: '产品相关',
      isPublic: true,
      myRole: 2,
      embeddingModelId: 'embedding-id',
      embeddingDimensions: 1024,
      isLock: false,
      createTime: '2026-09-02T00:00:00Z',
    } as never)
    vi.mocked(getWikiDocuments).mockResolvedValue({
      items: [],
      total: 0,
      pageNo: 1,
      pageSize: 20,
    } as never)
    vi.mocked(getWikiModelOptions).mockResolvedValue({
      embeddingModels: [{ id: 'embedding-id', name: 'text-embedding-3-large', modelKind: 'embedding' }],
      conversationModels: [{ id: 'metadata-id', name: 'gpt-4.1-mini', modelKind: 'conversation' }],
      rerankModels: [{ id: 'rerank-id', name: 'bge-reranker-v2', modelKind: 'rerank' }],
    })
  })

  it('渲染知识库信息并展示左侧菜单', async () => {
    renderDetail()

    expect((await screen.findAllByText('产品文档')).length).toBeGreaterThan(0)
    expect(screen.getByText('文件列表')).toBeInTheDocument()
    expect(screen.getByText('召回测试')).toBeInTheDocument()
    expect(screen.getByText('设置')).toBeInTheDocument()
  })

  it('文件列表 tab 展示文档表格与上传按钮', async () => {
    renderDetail('/team/3/wiki/7/files')

    expect(await screen.findByText('文件列表')).toBeInTheDocument()
    expect(await screen.findByText('上传文档')).toBeInTheDocument()
    expect(getWikiDocuments).toHaveBeenCalled()
  })

  it('召回测试 tab 展示占位', async () => {
    renderDetail('/team/3/wiki/7/recall')

    expect(await screen.findByText('召回测试功能建设中，敬请期待')).toBeInTheDocument()
  })

  it('管理员设置 tab 可编辑并保存时调用 updateWiki', async () => {
    renderDetail('/team/3/wiki/7/settings')

    const nameInput = await screen.findByLabelText('名称')
    expect(nameInput).toBeInTheDocument()
    fireEvent.change(nameInput, { target: { value: '改名后' } })
    fireEvent.click(screen.getAllByRole('button', { name: /保\s*存/ }).find((button) => button.textContent?.replace(/\s/g, '') === '保存')!)

    await waitFor(() => {
      expect(updateWiki).toHaveBeenCalledWith(7, expect.objectContaining({ name: '改名后' }))
    })
  })

  it('管理员设置 tab 加载团队向量模型并在设置中展示', async () => {
    renderDetail('/team/3/wiki/7/settings')

    const embeddingModel = await screen.findByLabelText('向量模型')
    expect(getWikiModelOptions).toHaveBeenCalledWith(3)

    fireEvent.mouseDown(embeddingModel)
    expect(await screen.findByRole('option', { name: 'text-embedding-3-large' })).toBeInTheDocument()
    // 元数据模型不在 wiki 设置页里了
    expect(screen.queryByLabelText('元数据模型')).toBeNull()
  })

  it('管理员设置 tab 模型选项加载失败时提示错误并禁用向量化配置保存', async () => {
    vi.mocked(getWikiModelOptions).mockRejectedValueOnce(new Error('model options failed'))
    renderDetail('/team/3/wiki/7/settings')

    expect(await screen.findByText('模型选项加载失败，请稍后重试')).toBeInTheDocument()
    expect(screen.getByLabelText('向量模型')).toBeDisabled()
    expect(screen.getByLabelText('向量维度')).toBeDisabled()
    expect(screen.getByRole('button', { name: '保存向量化配置' })).toBeDisabled()
    // 元数据/切片不再出现在 wiki 设置页
    expect(screen.queryByLabelText('元数据模型')).toBeNull()
    expect(screen.queryByLabelText('切片长度')).toBeNull()
    expect(screen.queryByLabelText('切片重叠')).toBeNull()
  })

  it('管理员选择 1024 预设维度并保存时只携带向量模型与维度调用 updateWikiEmbeddingConfig', async () => {
    renderDetail('/team/3/wiki/7/settings')

    const dimensionsInput = await screen.findByRole('combobox', { name: '向量维度' })
    fireEvent.focus(dimensionsInput)
    fireEvent.change(dimensionsInput, { target: { value: '1024' } })
    const dimensionsOption = await screen.findByRole('option', { name: '1024' })
    expect(dimensionsOption).toHaveTextContent('1024')
    fireEvent.click(dimensionsOption)
    fireEvent.change(dimensionsInput, { target: { value: '1024' } })
    fireEvent.click(screen.getByRole('button', { name: '保存向量化配置' }))

    await waitFor(() => {
      expect(updateWikiEmbeddingConfig).toHaveBeenCalledWith(7, {
        embeddingModelId: 'embedding-id',
        embeddingDimensions: 1024,
      })
    })
  })

  it('管理员手填 2000 维度并保存时只携带向量模型与维度调用 updateWikiEmbeddingConfig', async () => {
    renderDetail('/team/3/wiki/7/settings')

    const dimensionsInput = await screen.findByLabelText('向量维度')
    fireEvent.change(dimensionsInput, { target: { value: '2000' } })
    fireEvent.click(screen.getByRole('button', { name: '保存向量化配置' }))

    await waitFor(() => {
      expect(updateWikiEmbeddingConfig).toHaveBeenCalledWith(7, {
        embeddingModelId: 'embedding-id',
        embeddingDimensions: 2000,
      })
    })
  })

  it('管理员保存向量化配置失败后保留手填维度且不重载详情', async () => {
    vi.mocked(updateWikiEmbeddingConfig).mockRejectedValueOnce(new Error('save failed'))
    renderDetail('/team/3/wiki/7/settings')

    const dimensionsInput = await screen.findByLabelText('向量维度')
    fireEvent.change(dimensionsInput, { target: { value: '1536' } })
    fireEvent.click(screen.getByRole('button', { name: '保存向量化配置' }))

    await waitFor(() => {
      expect(updateWikiEmbeddingConfig).toHaveBeenCalled()
    })
    expect(dimensionsInput).toHaveValue('1536')
    expect(getWikiDetail).toHaveBeenCalledTimes(1)
  })

  it.each(['0', '2001', '4097', '1536.5', '-1'])('管理员输入无效维度 %s 时不保存向量化配置', async (dimensions) => {
    renderDetail('/team/3/wiki/7/settings')

    const dimensionsInput = await screen.findByLabelText('向量维度')
    fireEvent.change(dimensionsInput, { target: { value: dimensions } })
    fireEvent.click(screen.getByRole('button', { name: '保存向量化配置' }))

    await waitFor(() => {
      expect(dimensionsInput).toHaveAttribute('aria-invalid', 'true')
    })
    expect(updateWikiEmbeddingConfig).not.toHaveBeenCalled()
  })

  it('管理员保存向量化配置成功后刷新详情并回显新维度', async () => {
    vi.mocked(getWikiDetail)
      .mockResolvedValueOnce({
        wikiId: '7',
        teamId: '3',
        name: '产品文档',
        description: '产品相关',
        isPublic: true,
        myRole: 2,
        embeddingModelId: 'embedding-id',
        embeddingDimensions: 1024,
        isLock: false,
        createTime: '2026-09-02T00:00:00Z',
      } as never)
      .mockResolvedValueOnce({
        wikiId: '7',
        teamId: '3',
        name: '产品文档',
        description: '产品相关',
        isPublic: true,
        myRole: 2,
        embeddingModelId: 'embedding-id',
        embeddingDimensions: 2048,
        isLock: false,
        createTime: '2026-09-02T00:00:00Z',
      } as never)
    renderDetail('/team/3/wiki/7/settings')

    const dimensionsInput = await screen.findByLabelText('向量维度')
    fireEvent.click(screen.getByRole('button', { name: '保存向量化配置' }))

    await waitFor(() => {
      expect(getWikiDetail).toHaveBeenCalledTimes(2)
    })
    expect(dimensionsInput).toHaveValue('2048')
  })

  it('管理员保存向量化配置遇到锁定冲突后刷新并显示锁定提示', async () => {
    vi.mocked(getWikiDetail)
      .mockResolvedValueOnce({
        wikiId: '7',
        teamId: '3',
        name: '产品文档',
        description: '产品相关',
        isPublic: true,
        myRole: 2,
        embeddingModelId: 'embedding-id',
        embeddingDimensions: 1024,
        isLock: false,
        createTime: '2026-09-02T00:00:00Z',
      } as never)
      .mockResolvedValueOnce({
        wikiId: '7',
        teamId: '3',
        name: '产品文档',
        description: '产品相关',
        isPublic: true,
        myRole: 2,
        embeddingModelId: 'embedding-id',
        embeddingDimensions: 1024,
        isLock: true,
        createTime: '2026-09-02T00:00:00Z',
      } as never)
    vi.mocked(updateWikiEmbeddingConfig).mockRejectedValueOnce(new Error('知识库已有文档向量化，配置已锁定，不能修改.'))
    renderDetail('/team/3/wiki/7/settings')

    await screen.findByLabelText('向量维度')
    fireEvent.click(screen.getByRole('button', { name: '保存向量化配置' }))

    expect(await screen.findByText('向量化模型与维度已锁定')).toBeInTheDocument()
  })

  it('锁定的向量化配置不可编辑但仍可改名', async () => {
    vi.mocked(getWikiDetail).mockResolvedValue({
      wikiId: '7',
      teamId: '3',
      name: '产品文档',
      description: '产品相关',
      isPublic: true,
      myRole: 2,
      embeddingModelId: 'embedding-id',
      embeddingDimensions: 1024,
      isLock: true,
      createTime: '2026-09-02T00:00:00Z',
    } as never)
    renderDetail('/team/3/wiki/7/settings')

    expect(await screen.findByText('向量化模型与维度已锁定')).toBeInTheDocument()
    expect(screen.getByLabelText('向量模型')).toBeDisabled()
    expect(screen.getByLabelText('向量维度')).toBeDisabled()
    expect(screen.queryByLabelText('元数据模型')).toBeNull()
    expect(screen.queryByLabelText('切片长度')).toBeNull()
    expect(screen.queryByLabelText('切片重叠')).toBeNull()
    const nameInput = screen.getByLabelText('名称')
    expect(nameInput).toBeEnabled()
    fireEvent.change(nameInput, { target: { value: '锁定后仍可改名' } })
    fireEvent.click(screen.getAllByRole('button', { name: /保\s*存/ }).find((button) => button.textContent?.replace(/\s/g, '') === '保存')!)

    await waitFor(() => {
      expect(updateWiki).toHaveBeenCalledWith(7, expect.objectContaining({ name: '锁定后仍可改名' }))
    })
    const saveButton = screen.getByRole('button', { name: '保存向量化配置' })
    expect(saveButton).toBeDisabled()
    fireEvent.click(saveButton)

    expect(updateWikiEmbeddingConfig).not.toHaveBeenCalled()
  })

  it('管理员可选绑定重排序模型并保存', async () => {
    renderDetail('/team/3/wiki/7/settings')

    const rerankSelect = await screen.findByLabelText('重排序模型')
    fireEvent.mouseDown(rerankSelect)
    fireEvent.click(await screen.findByRole('option', { name: 'bge-reranker-v2' }))
    fireEvent.click(screen.getByRole('button', { name: '保存重排序模型' }))

    await waitFor(() => {
      expect(updateWikiRerankModel).toHaveBeenCalledWith(7, 'rerank-id')
    })
  })

  it('未选择重排序模型时保存为解绑（传 null）', async () => {
    renderDetail('/team/3/wiki/7/settings')

    expect(await screen.findByLabelText('重排序模型')).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: '保存重排序模型' }))

    await waitFor(() => {
      expect(updateWikiRerankModel).toHaveBeenCalledWith(7, null)
    })
  })

  it('知识库锁定后重排序模型仍可修改，向量化配置仍禁用', async () => {
    vi.mocked(getWikiDetail).mockResolvedValue({
      wikiId: '7',
      teamId: '3',
      name: '产品文档',
      description: '产品相关',
      isPublic: true,
      myRole: 2,
      embeddingModelId: 'embedding-id',
      embeddingDimensions: 1024,
      isLock: true,
      rerankModelId: null,
      createTime: '2026-09-02T00:00:00Z',
    } as never)
    renderDetail('/team/3/wiki/7/settings')

    expect(await screen.findByText('向量化模型与维度已锁定')).toBeInTheDocument()
    expect(screen.getByLabelText('向量模型')).toBeDisabled()
    const rerankSelect = screen.getByLabelText('重排序模型')
    expect(rerankSelect).toBeEnabled()
    fireEvent.mouseDown(rerankSelect)
    fireEvent.click(await screen.findByRole('option', { name: 'bge-reranker-v2' }))
    fireEvent.click(screen.getByRole('button', { name: '保存重排序模型' }))

    await waitFor(() => {
      expect(updateWikiRerankModel).toHaveBeenCalledWith(7, 'rerank-id')
    })
    expect(updateWikiEmbeddingConfig).not.toHaveBeenCalled()
  })

  it('Member 在设置 tab 为只读', async () => {
    vi.mocked(getWikiDetail).mockResolvedValue({
      wikiId: '7',
      teamId: '3',
      name: '产品文档',
      description: '',
      isPublic: false,
      myRole: 0,
      createTime: '2026-09-02T00:00:00Z',
    } as never)
    renderDetail('/team/3/wiki/7/settings')

    expect(await screen.findByText('仅团队管理员可管理知识库')).toBeInTheDocument()
  expect(getWikiModelOptions).not.toHaveBeenCalled()
    expect(screen.queryByRole('button', { name: '保存' })).toBeNull()
  })
})

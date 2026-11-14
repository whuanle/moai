import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import '@/i18n'
import { KnowledgeGraphImportPage } from '../KnowledgeGraphImportPage'
import { importKnowledgeGraphJson } from '@/api/knowledgeGraph'

vi.mock('@/api/knowledgeGraph', () => ({
  getKnowledgeGraphModelOptions: vi.fn().mockResolvedValue({ conversationModels: [{ id: 'm1', name: '桩模型' }], embeddingModels: [] }),
  importKnowledgeGraphFile: vi.fn(),
  importKnowledgeGraphJson: vi.fn(),
  getKnowledgeGraphImportJobs: vi.fn().mockResolvedValue({ items: [], total: 0 }),
  getKnowledgeGraphImportJob: vi.fn(),
  retryKnowledgeGraphImportJob: vi.fn(),
  cancelKnowledgeGraphImportJob: vi.fn(),
  uploadGraphFile: vi.fn(),
}))

vi.mock('@/api/kiota', () => ({ getApiClient: vi.fn(() => ({})) }))

const graph = {
  kgId: '9',
  teamId: '7',
  name: '导入测试图谱',
  myRole: 2,
  enabled: true,
} as never

describe('KnowledgeGraphImportPage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('渲染 AI 智能导入与 JSON 导入两个页签', () => {
    render(<MemoryRouter><KnowledgeGraphImportPage graph={graph} onChanged={() => {}} /></MemoryRouter>)
    expect(screen.getByText('AI 智能导入')).toBeInTheDocument()
    expect(screen.getByText('JSON 导入')).toBeInTheDocument()
  })

  it('AI 页签渲染导入任务历史（状态/进度/操作）', async () => {
    const { getKnowledgeGraphImportJobs } = await import('@/api/knowledgeGraph')
    vi.mocked(getKnowledgeGraphImportJobs).mockResolvedValue({
      total: 2,
      items: [
        { jobId: 12, fileName: '产品手册.docx', status: 'parsing', totalRows: 8, doneRows: 5 },
        { jobId: 11, fileName: '旧文档.pdf', status: 'failed', error: '文件不存在或内容提取失败' },
      ],
    })
    render(<MemoryRouter><KnowledgeGraphImportPage graph={graph} onChanged={() => {}} /></MemoryRouter>)
    expect(await screen.findByText('产品手册.docx')).toBeInTheDocument()
    expect(screen.getByText('AI 抽取')).toBeInTheDocument()
    // 失败任务展示错误与重试入口；进行中任务展示取消入口
    expect(screen.getByText('文件不存在或内容提取失败')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /重试/ })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /取消/ })).toBeInTheDocument()
  })

  it('JSON 页签提交导入：携带页面控件值并渲染逐条结果', async () => {
    vi.mocked(importKnowledgeGraphJson).mockResolvedValue({
      nodeCreatedCount: 1,
      nodeUpdatedCount: 0,
      nodeFailedCount: 1,
      edgeCreatedCount: 0,
      edgeSkippedCount: 0,
      edgeFailedCount: 0,
      createdEntityTypeNames: ['人员'],
      createdRelationTypeNames: [],
      results: [
        { kind: 'node', index: 0, ok: true, action: 'created', id: 'n-1' },
        { kind: 'node', index: 1, ok: false, message: 'key 在本请求中重复.' },
      ],
      duplicateSuspects: [
        { index: 0, name: '甲', kind: 'existing', score: 0.92, matchNodeId: 'n-old', matchName: '旧甲' },
      ],
    })
    render(<MemoryRouter><KnowledgeGraphImportPage graph={graph} onChanged={() => {}} /></MemoryRouter>)
    fireEvent.click(screen.getByText('JSON 导入'))
    const editor = await screen.findByPlaceholderText(/autoCreateTypes/)
    fireEvent.change(editor, { target: { value: '{"nodes":[{"entityTypeName":"人员","name":"甲"}]}' } })
    fireEvent.click(screen.getByRole('button', { name: /导\s*入/ }))

    await waitFor(() => expect(importKnowledgeGraphJson).toHaveBeenCalledWith(9, {
      content: '{"nodes":[{"entityTypeName":"人员","name":"甲"}]}',
      validateOnly: false,
      mode: 'upsert',
      autoCreateTypes: true,
      detectDuplicates: true,
    }))
    expect(await screen.findByText('导入结果')).toBeInTheDocument()
    expect(screen.getByText('人员')).toBeInTheDocument()
    expect(screen.getByText('key 在本请求中重复.')).toBeInTheDocument()
    expect(await screen.findByText('发现 1 组疑似重复实体')).toBeInTheDocument()
    expect(screen.getByText('旧甲')).toBeInTheDocument()
    // 疑似重复行可解析两端节点 id（新行 n-1 + 已有 n-old）→ 渲染「去合并」入口
    expect(await screen.findByRole('button', { name: /去合并/ })).toBeInTheDocument()
  })

  it('预检按钮以 validateOnly=true 调用且结果标题为预检', async () => {
    vi.mocked(importKnowledgeGraphJson).mockResolvedValue({
      nodeCreatedCount: 1, nodeUpdatedCount: 0, nodeFailedCount: 0,
      edgeCreatedCount: 0, edgeSkippedCount: 0, edgeFailedCount: 0,
      createdEntityTypeNames: [], createdRelationTypeNames: [], results: [],
    })
    render(<MemoryRouter><KnowledgeGraphImportPage graph={graph} onChanged={() => {}} /></MemoryRouter>)
    fireEvent.click(screen.getByText('JSON 导入'))
    const editor = await screen.findByPlaceholderText(/autoCreateTypes/)
    fireEvent.change(editor, { target: { value: '{"nodes":[{"entityTypeName":"人员","name":"甲"}]}' } })
    fireEvent.click(screen.getByRole('button', { name: /预检/ }))
    await waitFor(() => expect(importKnowledgeGraphJson).toHaveBeenCalledWith(9, expect.objectContaining({ validateOnly: true })))
    expect(await screen.findByText('预检结果（未落库，created 行 id 为空）')).toBeInTheDocument()
  })


  it('JSON 页签提供下载示例按钮', async () => {
    const created = vi.fn()
    const revoke = vi.fn()
    vi.stubGlobal('URL', Object.assign(URL, { createObjectURL: created, revokeObjectURL: revoke }))
    render(<MemoryRouter><KnowledgeGraphImportPage graph={graph} onChanged={() => {}} /></MemoryRouter>)
    fireEvent.click(screen.getByText('JSON 导入'))
    const btn = await screen.findByRole('button', { name: /下载示例 JSON/ })
    fireEvent.click(btn)
    expect(created).toHaveBeenCalled()
    vi.unstubAllGlobals()
  })
})

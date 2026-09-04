import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router'
import { WikiDocuments } from '../WikiDocuments'
import { getWikiDocuments } from '@/api/wiki'

vi.mock('@/api/wiki', () => ({
  getWikiDocuments: vi.fn().mockResolvedValue({
    wikiId: '5',
    myRole: 0,
    items: [
      { documentId: '11', wikiId: '5', title: '安装指南', createTime: '2026-09-02T00:00:00Z', updateTime: '2026-09-02T01:00:00Z' },
    ],
  }),
  getWikiDocumentDetail: vi.fn().mockResolvedValue({
    documentId: '11', wikiId: '5', title: '安装指南', content: '# 安装', myRole: 0,
    createTime: '2026-09-02T00:00:00Z', updateTime: '2026-09-02T01:00:00Z',
  }),
  createWikiDocument: vi.fn().mockResolvedValue(12),
  updateWikiDocument: vi.fn().mockResolvedValue(undefined),
  deleteWikiDocument: vi.fn().mockResolvedValue(undefined),
}))

vi.mock('@/api/kiota', () => ({
  getApiClient: vi.fn(() => ({})),
}))

function renderDocs() {
  return render(
    <MemoryRouter initialEntries={['/wiki/5']}>
      <Routes>
        <Route path="/wiki/:id" element={<WikiDocuments />} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('WikiDocuments', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(getWikiDocuments).mockResolvedValue({
      wikiId: '5',
      myRole: 0,
      items: [
        { documentId: '11', wikiId: '5', title: '安装指南', createTime: '2026-09-02T00:00:00Z', updateTime: '2026-09-02T01:00:00Z' },
      ],
    })
  })

  it('渲染文档列表与新建入口', async () => {
    renderDocs()

    await waitFor(() => {
      expect(getWikiDocuments).toHaveBeenCalledWith(5)
    })
    expect(await screen.findByText('安装指南')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: '新建文档' })).toBeInTheDocument()
    expect(screen.getByLabelText('删除')).toBeInTheDocument()
  })

  it('点击标题打开编辑器并回显正文', async () => {
    renderDocs()
    expect(await screen.findByText('安装指南')).toBeInTheDocument()

    screen.getByText('安装指南').click()
    expect(await screen.findByDisplayValue('安装指南')).toBeInTheDocument()
    expect(await screen.findByDisplayValue('# 安装')).toBeInTheDocument()
  })
})

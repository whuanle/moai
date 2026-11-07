import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes, useParams } from 'react-router'
import { TeamWikis } from '../TeamWikis'
import { getWikis, searchWikiTeamFiles } from '@/api/wiki'

vi.mock('@/api/wiki', () => ({
  getWikis: vi.fn(),
  createWiki: vi.fn().mockResolvedValue(1),
  updateWiki: vi.fn().mockResolvedValue(undefined),
  deleteWiki: vi.fn().mockResolvedValue(undefined),
  setWikiAvatar: vi.fn().mockResolvedValue(undefined),
  searchWikiTeamFiles: vi.fn().mockResolvedValue({ total: 0, items: [] }),
}))
vi.mock('@/utils/storage', () => ({
  resolveStorageUrl: vi.fn((v: string | null | undefined) => v ?? ''),
  uploadImageWithKey: vi.fn().mockResolvedValue({ objectKey: 'wiki/a.png', url: 'http://x/static/wiki/a.png' }),
}))

function renderWikis() {
  return render(
    <MemoryRouter>
      <TeamWikis teamId={7} />
    </MemoryRouter>,
  )
}

function DocumentProbe() {
  const params = useParams<{ wikiId: string; documentId: string; section?: string }>()
  return <div>probe-{params.wikiId}-{params.documentId}-{params.section ?? 'none'}</div>
}

function renderWikisWithRoutes() {
  return render(
    <MemoryRouter initialEntries={['/team/7/wiki']}>
      <Routes>
        <Route path="/team/:teamId/wiki" element={<TeamWikis teamId={7} />} />
        <Route path="/team/:teamId/wiki/:wikiId/document/:documentId/:section?" element={<DocumentProbe />} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('TeamWikis', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('卡片展示文件数/切片数/最近更新统计，不再展示公开状态', async () => {
    ;(getWikis as ReturnType<typeof vi.fn>).mockResolvedValue({
      teamId: '7',
      myRole: 2,
      items: [
        {
          wikiId: '9',
          teamId: '7',
          name: '产品文档',
          description: '产品相关',
          documentCount: 12,
          chunkCount: 345,
          lastDocumentUpdateTime: '2026-09-07T11:59:00Z',
          avatarPath: '',
          createTime: '2026-09-02T00:00:00Z',
        },
      ],
    })
    renderWikis()

    expect(await screen.findByText('产品文档')).toBeInTheDocument()
    // 统计并入名称下元信息行：文件数 · 切片数
    expect(screen.getByText('12 文件 · 345 切片')).toBeInTheDocument()
    // 底部页脚展示最近更新（只展示月日时分，按本地时区计算期望值）
    const d = new Date('2026-09-07T11:59:00Z')
    const pad = (n: number) => String(n).padStart(2, '0')
    expect(
      screen.getByText(`最近更新 ${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`),
    ).toBeInTheDocument()
    // 公开概念已移除
    expect(screen.queryByText('公开')).toBeNull()
    expect(screen.queryByText('私有')).toBeNull()
  })

  it('无文档时最近更新显示占位符', async () => {
    ;(getWikis as ReturnType<typeof vi.fn>).mockResolvedValue({
      teamId: '7',
      myRole: 2,
      items: [
        {
          wikiId: '10',
          teamId: '7',
          name: '空库',
          description: '',
          documentCount: 0,
          chunkCount: 0,
          lastDocumentUpdateTime: null,
          avatarPath: '',
          createTime: '2026-09-02T00:00:00Z',
        },
      ],
    })
    renderWikis()

    expect(await screen.findByText('空库')).toBeInTheDocument()
    expect(screen.getAllByText('-').length).toBeGreaterThan(0)
  })

  it('关键字搜索：回车过滤名称/描述，无匹配显示筛选空态', async () => {
    ;(getWikis as ReturnType<typeof vi.fn>).mockResolvedValue({
      teamId: '7',
      myRole: 2,
      items: [
        {
          wikiId: '11',
          teamId: '7',
          name: '产品文档',
          description: '产品相关说明',
          documentCount: 1,
          chunkCount: 2,
          lastDocumentUpdateTime: null,
          avatarPath: '',
          createTime: '2026-09-02T00:00:00Z',
        },
        {
          wikiId: '12',
          teamId: '7',
          name: '运维手册',
          description: '',
          documentCount: 3,
          chunkCount: 4,
          lastDocumentUpdateTime: null,
          avatarPath: '',
          createTime: '2026-09-02T00:00:00Z',
        },
      ],
    })
    renderWikis()

    expect(await screen.findByText('产品文档')).toBeInTheDocument()
    expect(screen.getByText('运维手册')).toBeInTheDocument()

    // 输入但不触发查询：卡片不过滤
    const input = screen.getByPlaceholderText('搜索知识库名称/描述/文件名')
    fireEvent.change(input, { target: { value: '运维' } })
    expect(screen.getByText('产品文档')).toBeInTheDocument()
    expect(screen.getByText('运维手册')).toBeInTheDocument()

    // 点击查询：只剩运维手册
    fireEvent.click(screen.getByRole('button', { name: /查\s*询/ }))
    expect(screen.queryByText('产品文档')).toBeNull()
    expect(screen.getByText('运维手册')).toBeInTheDocument()

    // 无匹配：筛选空态（区别于完全无数据的空态）
    fireEvent.change(input, { target: { value: '不存在的库' } })
    fireEvent.click(screen.getByRole('button', { name: /查\s*询/ }))
    expect(screen.getByText('没有符合筛选条件的知识库')).toBeInTheDocument()
    expect(screen.queryByText('还没有知识库，请在所属团队下新建一个')).toBeNull()
  })

  it('空关键字查询不调用跨库文件搜索', async () => {
    ;(getWikis as ReturnType<typeof vi.fn>).mockResolvedValue({
      teamId: '7',
      myRole: 2,
      items: [
        {
          wikiId: '20',
          teamId: '7',
          name: '产品文档',
          description: '',
          documentCount: 0,
          chunkCount: 0,
          lastDocumentUpdateTime: null,
          avatarPath: '',
          createTime: '2026-09-02T00:00:00Z',
        },
      ],
    })
    renderWikis()

    expect(await screen.findByText('产品文档')).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: /查\s*询/ }))
    await waitFor(() => expect(searchWikiTeamFiles).not.toHaveBeenCalled())
  })

  it('知识库命中时默认知识库 Tab，可手动切换到文件 Tab', async () => {
    ;(getWikis as ReturnType<typeof vi.fn>).mockResolvedValue({
      teamId: '7',
      myRole: 2,
      items: [
        {
          wikiId: '11',
          teamId: '7',
          name: '产品文档',
          description: '',
          documentCount: 1,
          chunkCount: 2,
          lastDocumentUpdateTime: null,
          avatarPath: '',
          createTime: '2026-09-02T00:00:00Z',
        },
        {
          wikiId: '12',
          teamId: '7',
          name: '运维手册',
          description: '',
          documentCount: 1,
          chunkCount: 2,
          lastDocumentUpdateTime: null,
          avatarPath: '',
          createTime: '2026-09-02T00:00:00Z',
        },
      ],
    })
    ;(searchWikiTeamFiles as ReturnType<typeof vi.fn>).mockResolvedValue({
      teamId: '7',
      total: 1,
      items: [
        { documentId: '101', wikiId: '12', wikiName: '运维手册', fileName: '手册.pdf', updateTime: '2026-10-09T10:00:00Z' },
      ],
    })
    renderWikis()

    expect(await screen.findByText('运维手册')).toBeInTheDocument()
    fireEvent.change(screen.getByPlaceholderText('搜索知识库名称/描述/文件名'), { target: { value: '手册' } })
    fireEvent.click(screen.getByRole('button', { name: /查\s*询/ }))

    // 知识库「运维手册」名称命中 → 默认仍落知识库 Tab；「产品文档」不含关键字被过滤
    expect(await screen.findByText('运维手册')).toBeInTheDocument()
    expect(screen.queryByText('产品文档')).toBeNull()
    expect(searchWikiTeamFiles).toHaveBeenCalledWith(7, '手册', 50)

    // 手动切到文件 Tab：卡片隐藏（文件行里的所属库名也叫「运维手册」，用卡片统计标签判断）
    fireEvent.click(screen.getByText('文件 1'))
    expect(await screen.findByText('手册.pdf')).toBeInTheDocument()
    expect(screen.queryByText('切片')).toBeNull()
  })

  it('知识库零命中且文件命中：自动落文件 Tab，点击文件行进入文档页', async () => {
    ;(getWikis as ReturnType<typeof vi.fn>).mockResolvedValue({
      teamId: '7',
      myRole: 2,
      items: [
        {
          wikiId: '11',
          teamId: '7',
          name: '产品文档',
          description: '',
          documentCount: 2,
          chunkCount: 3,
          lastDocumentUpdateTime: null,
          avatarPath: '',
          createTime: '2026-09-02T00:00:00Z',
        },
      ],
    })
    ;(searchWikiTeamFiles as ReturnType<typeof vi.fn>).mockResolvedValue({
      teamId: '7',
      total: 51,
      items: [
        { documentId: '101', wikiId: '11', wikiName: '产品文档', fileName: '使用手册.pdf', updateTime: '2026-10-09T10:00:00Z' },
        { documentId: '102', wikiId: '11', wikiName: '产品文档', fileName: '使用手册v2.pdf', updateTime: '2026-10-10T10:00:00Z' },
      ],
    })
    renderWikisWithRoutes()

    expect(await screen.findByText('产品文档')).toBeInTheDocument()
    fireEvent.change(screen.getByPlaceholderText('搜索知识库名称/描述/文件名'), { target: { value: '手册' } })
    fireEvent.click(screen.getByRole('button', { name: /查\s*询/ }))

    // 知识库零命中、文件有命中 → 自动落在文件 Tab（文件行带所属库名）
    expect(await screen.findByText('使用手册.pdf')).toBeInTheDocument()
    expect(screen.getByText('使用手册v2.pdf')).toBeInTheDocument()
    expect(screen.getByText('已按相关度排序，仅显示前 50 条')).toBeInTheDocument()
    expect(screen.queryByText('还没有知识库，请在所属团队下新建一个')).toBeNull()

    fireEvent.click(screen.getByText('使用手册.pdf'))
    expect(await screen.findByText('probe-11-101-embedding')).toBeInTheDocument()
  })
})

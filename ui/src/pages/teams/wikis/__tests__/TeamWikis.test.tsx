import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { TeamWikis } from '../TeamWikis'
import { getWikis } from '@/api/wiki'

vi.mock('@/api/wiki', () => ({
  getWikis: vi.fn(),
  createWiki: vi.fn().mockResolvedValue(1),
  updateWiki: vi.fn().mockResolvedValue(undefined),
  deleteWiki: vi.fn().mockResolvedValue(undefined),
  setWikiAvatar: vi.fn().mockResolvedValue(undefined),
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
    expect(screen.getByText('12')).toBeInTheDocument()
    expect(screen.getByText('345')).toBeInTheDocument()
    expect(screen.getByText('文件')).toBeInTheDocument()
    expect(screen.getByText('切片')).toBeInTheDocument()
    expect(screen.getByText('最近更新')).toBeInTheDocument()
    // 卡片只展示月日时分（formatDateTime 结果去掉年份前缀），按本地时区计算期望值
    const d = new Date('2026-09-07T11:59:00Z')
    const pad = (n: number) => String(n).padStart(2, '0')
    expect(screen.getByText(`${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`)).toBeInTheDocument()
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
})

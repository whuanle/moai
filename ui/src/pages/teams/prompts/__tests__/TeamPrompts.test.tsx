import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router'
import { TeamPrompts } from '../TeamPrompts'
import { getTeamPrompts } from '@/api/prompt'

vi.mock('@/api/prompt', () => ({
  getTeamPrompts: vi.fn().mockResolvedValue([]),
  getPromptDetail: vi.fn().mockResolvedValue(undefined),
  deletePrompt: vi.fn().mockResolvedValue(undefined),
}))

vi.mock('@/api/publication', () => ({
  applyPublication: vi.fn().mockResolvedValue('5'),
  withdrawPublication: vi.fn().mockResolvedValue(undefined),
}))

vi.mock('@/api/classify', () => ({
  classifyApi: {
    getClassifies: vi.fn().mockResolvedValue([{ classifyId: 3, name: '写作', emoji: '✍️' }]),
  },
  ClassifyType: { Prompt: 'prompt' },
  classifyLabel: (c: { emoji?: string | null; name?: string | null }) => [c.emoji, c.name].filter(Boolean).join(' '),
}))

vi.mock('@/api/kiota', () => ({
  getApiClient: vi.fn(() => ({})),
}))

function renderTeamPrompts() {
  return render(
    <MemoryRouter initialEntries={['/team/9']}>
      <Routes>
        <Route path="/team/:teamId" element={<TeamPrompts teamId={9} canManage />} />
        <Route path="/team/:teamId/prompt/new" element={<div data-testid="editor-page">editor-page</div>} />
        <Route path="/team/:teamId/prompt/:promptId/edit" element={<div data-testid="editor-page">editor-page</div>} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('TeamPrompts', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(getTeamPrompts).mockResolvedValue([
      { promptId: 1, name: '团队翻译', description: '团队提示词', promptClassId: 3, isPublic: true, counter: 3, teamId: 9, updateTime: '2026-09-15T00:00:00Z' },
      { promptId: 2, name: '周报助手', description: '', promptClassId: 0, isPublic: false, pendingPublicationId: '5', counter: 9, teamId: 9, updateTime: '2026-09-15T00:00:00Z' },
    ])
  })

  it('固定卡片列表展示状态与分类，不提供表格视图与条数统计', async () => {
    renderTeamPrompts()

    expect(await screen.findByText('团队翻译')).toBeInTheDocument()
    expect(screen.getByText('周报助手')).toBeInTheDocument()
    expect(screen.getByText('已上架')).toBeInTheDocument()
    expect(screen.getByText('待审核')).toBeInTheDocument()
    expect(screen.getByText('未分类')).toBeInTheDocument()
    // 无表格、无视图切换、头部不展示条数
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
    expect(screen.queryByText('表格')).not.toBeInTheDocument()
    expect(screen.queryByText('共 2 条')).not.toBeInTheDocument()
  })

  it('卡片按被使用次数降序排列，使用次数多的在前', async () => {
    renderTeamPrompts()

    await screen.findByText('周报助手')
    const ordered = screen.getAllByText(/^(团队翻译|周报助手)$/)
    expect(ordered.map((el) => el.textContent)).toEqual(['周报助手', '团队翻译'])
  })

  it('头部固定分类列表显示 emoji，点击分类按分类过滤', async () => {
    renderTeamPrompts()

    expect(await screen.findByText('全部')).toBeInTheDocument()
    // 分类 chip 展示「emoji + 名称」
    const chip = await screen.findByText('✍️ 写作')
    fireEvent.click(chip)
    await waitFor(() => {
      expect(getTeamPrompts).toHaveBeenLastCalledWith(9, { keywords: undefined, promptClassId: 3 })
    })
  })
})

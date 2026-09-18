import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router'
import { Skills } from '../Skills'
import { getMySkills, getSkillMarketList } from '@/api/skills'

vi.mock('@/api/skills', () => ({
  getMySkills: vi.fn().mockResolvedValue([]),
  getSkillMarketList: vi.fn().mockResolvedValue([]),
  getSkill: vi.fn().mockResolvedValue({}),
  deleteSkill: vi.fn().mockResolvedValue(undefined),
  downloadSkillFiles: vi.fn().mockResolvedValue(0),
}))

vi.mock('@/api/publication', () => ({
  applyPublication: vi.fn().mockResolvedValue('5'),
  withdrawPublication: vi.fn().mockResolvedValue(undefined),
}))

vi.mock('@/api/classify', () => ({
  classifyApi: {
    getClassifies: vi.fn().mockResolvedValue([{ classifyId: 3, name: '写作', emoji: '✍️' }]),
  },
  ClassifyType: { Prompt: 'prompt', Skill: 'skill' },
  classifyLabel: (c: { emoji?: string | null; name?: string | null }) => [c.emoji, c.name].filter(Boolean).join(' '),
}))

vi.mock('@/api/kiota', () => ({
  getApiClient: vi.fn(() => ({})),
}))

function renderSkills(initialPath = '/skill-market') {
  return render(
    <MemoryRouter initialEntries={[initialPath]}>
      <Routes>
        <Route path="/skill-market" element={<Skills />} />
        <Route path="/skills" element={<Skills />} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('Skills', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(getSkillMarketList).mockResolvedValue([
      { id: 's1', key: 'translate', name: '翻译技能', description: '中英互译', isPublic: true, classifyId: 3, fileCount: 2, updateTime: '2026-09-18T00:00:00Z' },
    ])
    vi.mocked(getMySkills).mockResolvedValue([])
  })

  it('市场 Tab 头部固定分类列表显示 emoji，点击分类按分类过滤', async () => {
    renderSkills()

    expect(await screen.findByText('全部')).toBeInTheDocument()
    // 分类 chip 展示「emoji + 名称」
    const chip = await screen.findByText('✍️ 写作')
    fireEvent.click(chip)
    await waitFor(() => {
      expect(getSkillMarketList).toHaveBeenLastCalledWith({ keywords: undefined, classifyId: 3 })
    })
  })

  it('切换到我的技能 Tab 后按 classifyId 加载个人列表', async () => {
    renderSkills('/skills')

    await waitFor(() => {
      expect(getMySkills).toHaveBeenCalledWith({ keywords: undefined, classifyId: undefined })
    })
  })
})

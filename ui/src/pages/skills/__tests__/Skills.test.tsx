import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router'
import { Skills } from '../Skills'
import { extractSkillPackage, getMySkills, getSkillMarketList, uploadSkillFile } from '@/api/skills'

vi.mock('@/api/skills', () => ({
  getMySkills: vi.fn().mockResolvedValue([]),
  getSkillMarketList: vi.fn().mockResolvedValue([]),
  getSkill: vi.fn().mockResolvedValue({}),
  deleteSkill: vi.fn().mockResolvedValue(undefined),
  downloadSkillFiles: vi.fn().mockResolvedValue(0),
  createSkill: vi.fn().mockResolvedValue('skill-id'),
  updateSkill: vi.fn().mockResolvedValue(undefined),
  uploadSkillFile: vi.fn().mockResolvedValue(1),
  extractSkillPackage: vi.fn().mockResolvedValue({ name: '', description: '', instructions: '', files: [] }),
  setSkillAvatar: vi.fn().mockResolvedValue(undefined),
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

describe('SkillEditModal 压缩包上传', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(getMySkills).mockResolvedValue([])
    vi.mocked(getSkillMarketList).mockResolvedValue([])
  })

  it('上传 zip 后触发解压并回填技能信息与文件清单', async () => {
    vi.mocked(extractSkillPackage).mockResolvedValue({
      name: '文档生成',
      description: '生成 docx',
      instructions: '# 用法',
      files: [
        { path: 'SKILL.md', fileId: 11, fileName: 'SKILL.md' },
        { path: 'scripts/run.py', fileId: 12, fileName: 'run.py' },
      ],
    })
    renderSkills('/skills')
    fireEvent.click(await screen.findByRole('button', { name: /新建技能/ }))
    await screen.findByRole('dialog')
    // 弹窗内有头像与技能包两个文件输入，按 accept 锁定 zip 技能包输入
    const zipInput = document.querySelector('input[type="file"][accept*="zip"]') as HTMLInputElement
    expect(zipInput).toBeTruthy()
    fireEvent.change(zipInput, {
      target: { files: [new File(['x'], 'pack.zip', { type: 'application/zip' })] },
    })
    await waitFor(() => {
      expect(uploadSkillFile).toHaveBeenCalled()
      expect(extractSkillPackage).toHaveBeenCalledWith(1)
    })
    // SKILL.md 信息回填表单
    const nameInput = document.querySelector('#name') as HTMLInputElement
    const descInput = document.querySelector('#description') as HTMLTextAreaElement
    await waitFor(() => {
      expect(nameInput.value).toBe('文档生成')
    })
    expect(descInput.value).toBe('生成 docx')
  })
})

import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router'
import { PromptEditor } from '../PromptEditor'
import { createPrompt, getPromptDetail, updatePrompt, setPromptAvatar } from '@/api/prompt'
import { classifyApi } from '@/api/classify'

vi.mock('@/api/prompt', () => ({
  getPromptDetail: vi.fn().mockResolvedValue(undefined),
  createPrompt: vi.fn().mockResolvedValue(9),
  updatePrompt: vi.fn().mockResolvedValue(undefined),
  setPromptAvatar: vi.fn().mockResolvedValue(undefined),
  deletePrompt: vi.fn().mockResolvedValue(undefined),
  getMyPrompts: vi.fn().mockResolvedValue([]),
}))

vi.mock('@/api/classify', () => ({
  classifyApi: {
    getClassifies: vi.fn().mockResolvedValue([{ classifyId: 3, name: '写作' }]),
  },
  ClassifyType: { Prompt: 'prompt' },
}))

vi.mock('@/api/kiota', () => ({
  getApiClient: vi.fn(() => ({})),
}))

function renderEditor(initialPath: string) {
  return render(
    <MemoryRouter initialEntries={[initialPath]}>
      <Routes>
        <Route path="/prompts/new" element={<PromptEditor />} />
        <Route path="/prompts/:promptId/edit" element={<PromptEditor />} />
        <Route path="/team/:teamId/prompt/new" element={<PromptEditor />} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('PromptEditor', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('新建页输入 Markdown 内容，右侧实时预览渲染标题', async () => {
    renderEditor('/prompts/new')

    fireEvent.change(await screen.findByPlaceholderText('请输入提示词名称'), { target: { value: '翻译助手' } })
    const editor = screen.getByPlaceholderText(/支持 Markdown 语法/)
    fireEvent.change(editor, { target: { value: '# 翻译官\n你是翻译助手' } })

    expect(await screen.findByRole('heading', { level: 1, name: '翻译官' })).toBeInTheDocument()
    expect(screen.getByText('你是翻译助手')).toBeInTheDocument()
  })

  it('新建提交以 teamId=0 调用创建接口', async () => {
    renderEditor('/prompts/new')

    fireEvent.change(await screen.findByPlaceholderText('请输入提示词名称'), { target: { value: '代码评审' } })
    fireEvent.change(screen.getByPlaceholderText(/支持 Markdown 语法/), { target: { value: '请评审代码' } })
    fireEvent.click(screen.getAllByRole('button', { name: '新建提示词' })[0])

    await waitFor(() => {
      expect(createPrompt).toHaveBeenCalledWith(expect.objectContaining({ teamId: 0, name: '代码评审', content: '请评审代码' }))
    })
  })

  it('编辑页回填详情并调用更新接口', async () => {
    vi.mocked(getPromptDetail).mockResolvedValue({
      promptId: 5, name: '旧名称', description: '', content: '# 旧内容', promptClassId: 0, isPublic: false, teamId: 0, avatarPath: '',
    })
    renderEditor('/prompts/5/edit')

    expect(await screen.findByPlaceholderText('请输入提示词名称')).toHaveValue('旧名称')
    await waitFor(() => {
      expect(screen.getByRole('heading', { level: 1, name: '旧内容' })).toBeInTheDocument()
    })

    fireEvent.change(screen.getByPlaceholderText(/支持 Markdown 语法/), { target: { value: '# 新内容' } })
    // antd 两字按钮自动插入空格：保 存
    fireEvent.click(screen.getAllByRole('button', { name: /保\s*存/ })[0])

    await waitFor(() => {
      expect(updatePrompt).toHaveBeenCalledWith(5, expect.objectContaining({ name: '旧名称', content: '# 新内容' }))
    })
    expect(setPromptAvatar).not.toHaveBeenCalled()
  })

  it('团队新建以路由团队 id 调用创建接口', async () => {
    renderEditor('/team/7/prompt/new')

    fireEvent.change(await screen.findByPlaceholderText('请输入提示词名称'), { target: { value: '团队周报' } })
    fireEvent.change(screen.getByPlaceholderText(/支持 Markdown 语法/), { target: { value: '生成周报' } })
    fireEvent.click(screen.getAllByRole('button', { name: '新建提示词' })[0])

    await waitFor(() => {
      expect(createPrompt).toHaveBeenCalledWith(expect.objectContaining({ teamId: 7, name: '团队周报' }))
    })
  })
})

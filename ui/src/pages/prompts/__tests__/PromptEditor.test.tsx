import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router'
import { PromptEditor } from '../PromptEditor'
import { createPrompt, getPromptDetail, updatePrompt, setPromptAvatar } from '@/api/prompt'

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
  classifyLabel: (c: { emoji?: string | null; name?: string | null }) => [c.emoji, c.name].filter(Boolean).join(' '),
}))

vi.mock('@/api/kiota', () => ({
  getApiClient: vi.fn(() => ({})),
}))

// Vditor 依赖浏览器 API 与 WASM，jsdom 下以受控 textarea 替身验证接线（value/onChange/mode/maxLength 透传）
vi.mock('@/components/VditorMarkdownEditor', () => ({
  VditorMarkdownEditor: ({
    value,
    onChange,
    mode,
    placeholder,
    maxLength,
  }: {
    value: string
    onChange: (v: string) => void
    mode?: string
    placeholder?: string
    maxLength?: number
  }) => (
    <textarea
      data-testid="vditor-markdown-editor"
      data-mode={mode}
      placeholder={placeholder}
      maxLength={maxLength}
      value={value}
      onChange={(e) => onChange(e.target.value)}
    />
  ),
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

function getEditor() {
  return screen.getByTestId('vditor-markdown-editor') as HTMLTextAreaElement
}

describe('PromptEditor', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    localStorage.clear()
  })

  it('新建页所见即所得编辑器输入内容，保存以 teamId=0 调用创建接口', async () => {
    renderEditor('/prompts/new')

    fireEvent.change(await screen.findByPlaceholderText('请输入提示词名称'), { target: { value: '翻译助手' } })
    fireEvent.change(getEditor(), { target: { value: '# 翻译官\n你是翻译助手' } })
    fireEvent.click(screen.getAllByRole('button', { name: '新建提示词' })[0])

    await waitFor(() => {
      expect(createPrompt).toHaveBeenCalledWith(
        expect.objectContaining({ teamId: 0, name: '翻译助手', content: '# 翻译官\n你是翻译助手' }),
      )
    })
  })

  it('编辑器透传内容上限 10000', async () => {
    renderEditor('/prompts/new')

    expect(await getEditor()).toHaveAttribute('maxlength', '10000')
  })

  it('内容为空时保存被拦截，不调用创建接口', async () => {
    renderEditor('/prompts/new')

    fireEvent.change(await screen.findByPlaceholderText('请输入提示词名称'), { target: { value: '空内容' } })
    fireEvent.click(screen.getAllByRole('button', { name: '新建提示词' })[0])

    await waitFor(() => {
      expect(createPrompt).not.toHaveBeenCalled()
    })
  })

  it('编辑页回填详情到所见即所得编辑器并调用更新接口', async () => {
    vi.mocked(getPromptDetail).mockResolvedValue({
      promptId: 5, name: '旧名称', description: '', content: '# 旧内容', promptClassId: 0, isPublic: false, teamId: 0, avatarPath: '',
    })
    renderEditor('/prompts/5/edit')

    expect(await screen.findByPlaceholderText('请输入提示词名称')).toHaveValue('旧名称')
    await waitFor(() => {
      expect(getEditor()).toHaveValue('# 旧内容')
    })

    fireEvent.change(getEditor(), { target: { value: '# 新内容' } })
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
    fireEvent.change(getEditor(), { target: { value: '生成周报' } })
    fireEvent.click(screen.getAllByRole('button', { name: '新建提示词' })[0])

    await waitFor(() => {
      expect(createPrompt).toHaveBeenCalledWith(expect.objectContaining({ teamId: 7, name: '团队周报' }))
    })
  })

  it('模式切换：所见即所得与纯源码互切并透传给编辑器', async () => {
    renderEditor('/prompts/new')

    expect(await getEditor()).toHaveAttribute('data-mode', 'ir')
    // 切换器为图标（眼睛/代码），选项顺序固定：0=所见即所得、1=纯源码
    const segmentItems = () => document.querySelectorAll('.ant-segmented-item')
    fireEvent.click(segmentItems()[1])
    expect(getEditor()).toHaveAttribute('data-mode', 'sv')
    fireEvent.click(segmentItems()[0])
    expect(getEditor()).toHaveAttribute('data-mode', 'ir')
  })

  it('输入内容自动落浏览器草稿，重开页面恢复', async () => {
    const { unmount } = renderEditor('/prompts/new')

    fireEvent.change(await screen.findByPlaceholderText('请输入提示词名称'), { target: { value: '草稿用户' } })
    fireEvent.change(getEditor(), { target: { value: '# 未保存内容' } })
    await waitFor(() => {
      expect(localStorage.getItem('moai-prompt-draft:new:t0')).not.toBeNull()
    })
    unmount()

    renderEditor('/prompts/new')
    expect(await screen.findByPlaceholderText('请输入提示词名称')).toHaveValue('草稿用户')
    await waitFor(() => {
      expect(getEditor()).toHaveValue('# 未保存内容')
    })
    // 「已恢复草稿」toast 依赖真实应用的 FeedbackBridge，jsdom 下不渲染，浏览器走查验证
  })

  it('编辑页草稿覆盖服务端内容（未保存修改不丢失）', async () => {
    vi.mocked(getPromptDetail).mockResolvedValue({
      promptId: 5, name: '服务端名称', description: '', content: '# 服务端内容', promptClassId: 0, isPublic: false, teamId: 0, avatarPath: '',
    })
    localStorage.setItem('moai-prompt-draft:edit:5', JSON.stringify({ name: '服务端名称', content: '# 本地草稿内容' }))
    renderEditor('/prompts/5/edit')

    await waitFor(() => {
      expect(getEditor()).toHaveValue('# 本地草稿内容')
    })
    expect(screen.getByPlaceholderText('请输入提示词名称')).toHaveValue('服务端名称')
  })

  it('保存成功后清除草稿', async () => {
    renderEditor('/prompts/new')

    fireEvent.change(await screen.findByPlaceholderText('请输入提示词名称'), { target: { value: '保存即清' } })
    fireEvent.change(getEditor(), { target: { value: '内容' } })
    fireEvent.click(screen.getAllByRole('button', { name: '新建提示词' })[0])

    await waitFor(() => {
      expect(createPrompt).toHaveBeenCalled()
    })
    await waitFor(() => {
      expect(localStorage.getItem('moai-prompt-draft:new:t0')).toBeNull()
    })
  })
})

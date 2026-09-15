import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { Prompts } from '../Prompts'
import { getMyPrompts, createPrompt, deletePrompt } from '@/api/prompt'
import { applyPublication } from '@/api/publication'
import { classifyApi } from '@/api/classify'

vi.mock('@/api/prompt', () => ({
  getMyPrompts: vi.fn().mockResolvedValue([]),
  getPromptDetail: vi.fn().mockResolvedValue({
    promptId: 1, name: '翻译助手', description: '中英互译', content: '你是翻译官', promptClassId: 0, isPublic: false, teamId: 0,
  }),
  createPrompt: vi.fn().mockResolvedValue(9),
  updatePrompt: vi.fn().mockResolvedValue(undefined),
  deletePrompt: vi.fn().mockResolvedValue(undefined),
}))

vi.mock('@/api/publication', () => ({
  applyPublication: vi.fn().mockResolvedValue('5'),
  withdrawPublication: vi.fn().mockResolvedValue(undefined),
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

function renderPrompts() {
  return render(
    <MemoryRouter>
      <Prompts />
    </MemoryRouter>,
  )
}

describe('Prompts', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(getMyPrompts).mockResolvedValue([
      { promptId: 1, name: '翻译助手', description: '中英互译', promptClassId: 3, isPublic: true, counter: 7, teamId: 0, updateTime: '2026-09-15T00:00:00Z' },
      { promptId: 2, name: '周报生成', description: '', promptClassId: 0, isPublic: false, pendingPublicationId: '5', counter: 0, teamId: 0, updateTime: '2026-09-15T00:00:00Z' },
    ])
  })

  it('渲染我的提示词列表：已上架/待审核状态标签', async () => {
    renderPrompts()

    await waitFor(() => {
      expect(getMyPrompts).toHaveBeenCalledWith({ keywords: undefined, promptClassId: undefined })
    })
    expect(await screen.findByText(/翻译助手/)).toBeInTheDocument()
    expect(screen.getByText(/周报生成/)).toBeInTheDocument()
    expect(screen.getByText('已上架')).toBeInTheDocument()
    expect(screen.getByText('待审核')).toBeInTheDocument()
  })

  it('新建提示词提交时创建个人提示词（teamId=0）', async () => {
    vi.mocked(getMyPrompts).mockResolvedValue([])
    renderPrompts()

    fireEvent.click(await screen.findByRole('button', { name: '新建提示词' }))
    const modal = await screen.findByRole('dialog')
    fireEvent.change(within(modal).getByPlaceholderText('请输入提示词名称'), { target: { value: '代码评审' } })
    fireEvent.change(within(modal).getByPlaceholderText('请输入提示词内容'), { target: { value: '请评审以下代码' } })
    fireEvent.click(within(modal).getByRole('button', { name: /确\s*定/ }))

    await waitFor(() => {
      expect(createPrompt).toHaveBeenCalledWith(expect.objectContaining({ teamId: 0, name: '代码评审' }))
    })
  })

  it('未上架提示词可申请上架，提交走 publication（resourceType=prompt）', async () => {
    vi.mocked(getMyPrompts).mockResolvedValue([
      { promptId: 2, name: '周报生成', description: '', promptClassId: 0, isPublic: false, counter: 0, teamId: 0, updateTime: '2026-09-15T00:00:00Z' },
    ])
    renderPrompts()

    fireEvent.click(await screen.findByLabelText('申请上架'))
    const modal = await screen.findByRole('dialog')
    fireEvent.click(within(modal).getByRole('button', { name: '提交申请' }))

    await waitFor(() => {
      expect(applyPublication).toHaveBeenCalledWith(expect.objectContaining({ resourceType: 'prompt', resourceId: '2' }))
    })
  })

  it('删除提示词需 Popconfirm 确认', async () => {
    renderPrompts()

    const del = (await screen.findAllByLabelText('删除'))[0]
    fireEvent.click(del)
    // 测试环境未包裹 AppProviders，antd 默认英文 locale，Popconfirm 确认按钮为 OK
    fireEvent.click(await screen.findByRole('button', { name: 'OK' }))

    await waitFor(() => {
      expect(deletePrompt).toHaveBeenCalledWith(1)
    })
  })
})

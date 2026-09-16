import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router'
import { PromptCenter } from '../PromptCenter'
import { getMyPrompts, getPromptMarketList, getPromptDetail, deletePrompt } from '@/api/prompt'
import { applyPublication } from '@/api/publication'

vi.mock('@/api/prompt', () => ({
  getMyPrompts: vi.fn().mockResolvedValue([]),
  getPromptMarketList: vi.fn().mockResolvedValue([]),
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

function renderCenter(initialPath = '/prompt-market') {
  return render(
    <MemoryRouter initialEntries={[initialPath]}>
      <Routes>
        <Route path="/prompts" element={<PromptCenter />} />
        <Route path="/prompt-market" element={<PromptCenter />} />
        <Route path="/prompts/new" element={<div data-testid="editor-page">editor-page</div>} />
        <Route path="/prompts/:promptId/edit" element={<div data-testid="editor-page">editor-page</div>} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('PromptCenter', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(getMyPrompts).mockResolvedValue([
      { promptId: 1, name: '翻译助手', description: '中英互译', promptClassId: 3, isPublic: true, counter: 7, teamId: 0, updateTime: '2026-09-15T00:00:00Z' },
      { promptId: 2, name: '周报生成', description: '', promptClassId: 0, isPublic: false, pendingPublicationId: '5', counter: 0, teamId: 0, updateTime: '2026-09-15T00:00:00Z' },
    ])
    vi.mocked(getPromptMarketList).mockResolvedValue([
      { promptId: 11, name: '市场翻译', description: '市场提示词', promptClassId: 3, isPublic: true, counter: 4, teamId: 9, createUserName: 'alice', updateTime: '2026-09-15T00:00:00Z' },
    ])
  })

  it('默认进入提示词市场 tab：加载市场列表并按分类列表 + 卡片展示', async () => {
    renderCenter()

    await waitFor(() => {
      expect(getPromptMarketList).toHaveBeenCalledWith({ keywords: undefined, promptClassId: undefined })
    })
    expect(getMyPrompts).not.toHaveBeenCalled()
    expect(await screen.findByText('市场翻译')).toBeInTheDocument()
    // 头部 tab 与分类列表（卡片分类 Tag 与筛选 chip 同名，用 checkable 选择器区分）
    expect(screen.getByRole('tab', { name: '提示词市场' })).toBeInTheDocument()
    expect(screen.getByRole('tab', { name: '我的提示词' })).toBeInTheDocument()
    expect(screen.getByText('全部')).toBeInTheDocument()
    expect(screen.getAllByText('写作')[0]?.closest('.ant-tag-checkable')).toBeTruthy()
  })

  it('点击分类标签按分类过滤市场列表', async () => {
    renderCenter()

    // 分类 chip 渲染为 <span class="ant-tag ant-tag-checkable"><span>写作</span></span>，与卡片分类 Tag 同名，取 checkable 外层节点点击
    const candidates = await screen.findAllByText('写作')
    const chip = candidates.map((el) => el.closest('.ant-tag-checkable')).find(Boolean)
    expect(chip).toBeTruthy()
    fireEvent.click(chip as Element)
    await waitFor(() => {
      expect(getPromptMarketList).toHaveBeenLastCalledWith({ keywords: undefined, promptClassId: 3 })
    })
  })

  it('切换到我的提示词 tab：加载个人列表并显示上架状态标签', async () => {
    renderCenter()

    fireEvent.click(await screen.findByRole('tab', { name: '我的提示词' }))
    await waitFor(() => {
      expect(getMyPrompts).toHaveBeenCalledWith({ keywords: undefined, promptClassId: undefined })
    })
    expect(await screen.findByText(/翻译助手/)).toBeInTheDocument()
    expect(screen.getByText(/周报生成/)).toBeInTheDocument()
    expect(screen.getByText('已上架')).toBeInTheDocument()
    expect(screen.getByText('待审核')).toBeInTheDocument()
  })

  it('我的提示词 tab 点击新建跳转独立编辑器页', async () => {
    renderCenter('/prompts')

    fireEvent.click(await screen.findByRole('button', { name: '新建提示词' }))
    expect(await screen.findByTestId('editor-page')).toBeInTheDocument()
  })

  it('我的提示词 tab 未上架提示词可申请上架，提交走 publication（resourceType=prompt）', async () => {
    vi.mocked(getMyPrompts).mockResolvedValue([
      { promptId: 2, name: '周报生成', description: '', promptClassId: 0, isPublic: false, counter: 0, teamId: 0, updateTime: '2026-09-15T00:00:00Z' },
    ])
    renderCenter('/prompts')

    fireEvent.click(await screen.findByLabelText('申请上架'))
    const modal = await screen.findByRole('dialog')
    fireEvent.click(within(modal).getByRole('button', { name: '提交申请' }))

    await waitFor(() => {
      expect(applyPublication).toHaveBeenCalledWith(expect.objectContaining({ resourceType: 'prompt', resourceId: '2' }))
    })
  })

  it('我的提示词 tab 删除提示词需 Popconfirm 确认', async () => {
    renderCenter('/prompts')

    const del = (await screen.findAllByLabelText('删除'))[0]
    fireEvent.click(del)
    // 测试环境未包裹 AppProviders，antd 默认英文 locale，Popconfirm 确认按钮为 OK
    fireEvent.click(await screen.findByRole('button', { name: 'OK' }))

    await waitFor(() => {
      expect(deletePrompt).toHaveBeenCalledWith(1)
    })
  })

  it('市场卡片点击使用打开详情弹窗', async () => {
    renderCenter()

    fireEvent.click(await screen.findByRole('button', { name: /使用/ }))
    await waitFor(() => {
      expect(getPromptDetail).toHaveBeenCalledWith(11)
    })
    expect(await screen.findByRole('dialog')).toBeInTheDocument()
  })
})

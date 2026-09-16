import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router'
import { AppChat } from '../AppChat'
import {
  createAppSession,
  getAppDetail,
  getAppSessionMessages,
  getAppSessions,
  getAppUserConfig,
  updateAppSessionPrompt,
} from '@/api/app'
import { getMyPrompts, getTeamPrompts } from '@/api/prompt'
import { runAppChat } from '@/api/agentChat'

vi.mock('@/api/app', () => ({
  getAppDetail: vi.fn(),
  getAppSessions: vi.fn(),
  createAppSession: vi.fn(),
  getAppSessionMessages: vi.fn(),
  deleteAppSession: vi.fn().mockResolvedValue(undefined),
  updateAppSessionPrompt: vi.fn().mockResolvedValue(undefined),
  getAppUserConfig: vi.fn(),
  saveAppUserConfig: vi.fn().mockResolvedValue(undefined),
}))

vi.mock('@/api/prompt', () => ({
  getMyPrompts: vi.fn(),
  getTeamPrompts: vi.fn(),
}))

vi.mock('@/api/agentChat', () => ({
  createAppChatAgent: vi.fn().mockReturnValue({}),
  runAppChat: vi.fn(),
  abortAppChat: vi.fn(),
}))

function renderPage() {
  return render(
    <MemoryRouter initialEntries={['/team/3/app/a1/chat']}>
      <Routes>
        <Route path="/team/:teamId/app/:appId/chat" element={<AppChat />} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('AppChat（Agent 应用对话页）', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(getAppDetail).mockResolvedValue({ appId: 'a1', name: '客服助手' } as never)
    vi.mocked(getAppSessions).mockResolvedValue([
      { sessionId: 's1', title: '第一段对话', lastMessageTime: '2026-09-11T10:00:00Z' },
    ] as never)
    vi.mocked(createAppSession).mockResolvedValue('s2')
    vi.mocked(getAppSessionMessages).mockResolvedValue([])
    vi.mocked(getMyPrompts).mockResolvedValue([
      { promptId: 11, name: '写作专家', description: '文案润色', teamId: 0 },
    ] as never)
    vi.mocked(getTeamPrompts).mockResolvedValue([
      { promptId: 22, name: '客服专家', description: '售后话术', teamId: 3 },
    ] as never)
    vi.mocked(updateAppSessionPrompt).mockResolvedValue(undefined)
    vi.mocked(getAppUserConfig).mockResolvedValue({ promptId: 0, skills: [], lockedSkills: [] })
  })

  it('加载后展示会话列表，且为沉浸式布局（空态欢迎语 + 建议问题，无面包屑/大标题）', async () => {
    renderPage()
    await waitFor(() => {
      expect(screen.getByText('第一段对话')).toBeInTheDocument()
    })
    expect(screen.getByText('有什么可以帮你？')).toBeInTheDocument()
    expect(screen.getByText('帮我总结一份文档的核心要点')).toBeInTheDocument()
    expect(screen.queryByText('应用对话')).not.toBeInTheDocument()
    expect(document.querySelector('.ant-breadcrumb')).toBeNull()
    expect(document.querySelector('.moai-chat')).not.toBeNull()
  })

  it('发送消息时创建会话并流式追加回复', async () => {
    vi.mocked(runAppChat).mockImplementation(async (_agent, _text, handlers) => {
      handlers.onDelta?.('你好')
      handlers.onDelta?.('你好，很高兴为您服务')
      handlers.onDone?.()
    })

    renderPage()
    await waitFor(() => expect(screen.getByText('第一段对话')).toBeInTheDocument())

    const textarea = screen.getByPlaceholderText(/输入消息/)
    fireEvent.change(textarea, { target: { value: '在吗' } })
    fireEvent.click(screen.getByRole('button', { name: '发送' }))

    await waitFor(() => {
      expect(createAppSession).toHaveBeenCalledWith('a1', undefined, 0)
    })
    expect(runAppChat).toHaveBeenCalled()
    await waitFor(() => {
      expect(screen.getByText(/很高兴为您服务/)).toBeInTheDocument()
    })
    expect(document.querySelector('.moai-chat__bubble')?.textContent).toBe('在吗')
  })

  it('助手回复以 Markdown 渲染', async () => {
    vi.mocked(runAppChat).mockImplementation(async (_agent, _text, handlers) => {
      handlers.onDelta?.('**加粗** 与 `代码`')
    })

    renderPage()
    await waitFor(() => expect(screen.getByText('第一段对话')).toBeInTheDocument())

    fireEvent.change(screen.getByPlaceholderText(/输入消息/), { target: { value: 'x' } })
    fireEvent.click(screen.getByRole('button', { name: '发送' }))

    await waitFor(() => {
      expect(screen.getByText('加粗').tagName).toBe('STRONG')
    })
    expect(screen.getByText('代码').tagName).toBe('CODE')
  })

  it('点击展开专家侧边栏，展示个人与团队提示词', async () => {
    renderPage()
    await waitFor(() => expect(screen.getByText('第一段对话')).toBeInTheDocument())

    fireEvent.click(screen.getByRole('button', { name: '专家' }))

    await waitFor(() => {
      expect(screen.getByText('写作专家')).toBeInTheDocument()
    })
    expect(screen.getByText('客服专家')).toBeInTheDocument()
    expect(screen.getByText('文案润色')).toBeInTheDocument()
    // 个人/团队来源标签
    expect(screen.getByText('个人')).toBeInTheDocument()
    expect(screen.getByText('团队')).toBeInTheDocument()
  })

  it('未发送消息时选择专家：本地记录，首轮创建会话时一并绑定', async () => {
    renderPage()
    await waitFor(() => expect(screen.getByText('第一段对话')).toBeInTheDocument())

    fireEvent.click(screen.getByRole('button', { name: '专家' }))
    await waitFor(() => expect(screen.getByText('写作专家')).toBeInTheDocument())
    fireEvent.click(screen.getByText('写作专家'))

    fireEvent.change(screen.getByPlaceholderText(/输入消息/), { target: { value: '你好' } })
    fireEvent.click(screen.getByRole('button', { name: '发送' }))

    await waitFor(() => {
      expect(createAppSession).toHaveBeenCalledWith('a1', undefined, 11)
    })
    expect(updateAppSessionPrompt).not.toHaveBeenCalled()
    // 输入区上方展示当前专家提示条
    expect(screen.getByText('当前专家')).toBeInTheDocument()
    // 列表项 + 当前专家提示条各渲染一次
    expect(screen.getAllByText('写作专家').length).toBeGreaterThanOrEqual(2)
  })

  it('已有会话时选择专家：调用绑定接口，再次点击取消', async () => {
    renderPage()
    await waitFor(() => expect(screen.getByText('第一段对话')).toBeInTheDocument())

    fireEvent.click(screen.getByText('第一段对话'))
    await waitFor(() => expect(getAppSessionMessages).toHaveBeenCalledWith('s1'))

    fireEvent.click(screen.getByRole('button', { name: '专家' }))
    await waitFor(() => expect(screen.getByText('写作专家')).toBeInTheDocument())
    fireEvent.click(screen.getByText('写作专家'))

    await waitFor(() => {
      expect(updateAppSessionPrompt).toHaveBeenCalledWith('s1', 11)
    })

    // 再次点击列表项（第一处渲染）取消绑定
    fireEvent.click(screen.getAllByText('写作专家')[0])
    await waitFor(() => {
      expect(updateAppSessionPrompt).toHaveBeenCalledWith('s1', 0)
    })
  })
})

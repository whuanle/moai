import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router'
import { AppChat } from '../AppChat'
import {
  createAppSession,
  getAppDetail,
  getAppSessionMessages,
  getAppSessions,
} from '@/api/app'
import { runAppChat } from '@/api/agentChat'

vi.mock('@/api/app', () => ({
  getAppDetail: vi.fn(),
  getAppSessions: vi.fn(),
  createAppSession: vi.fn(),
  getAppSessionMessages: vi.fn(),
  deleteAppSession: vi.fn().mockResolvedValue(undefined),
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
      expect(createAppSession).toHaveBeenCalledWith('a1')
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
})

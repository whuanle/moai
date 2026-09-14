import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { AppDebugChat } from '../chat/AppDebugChat'
import { createDebugSession } from '@/api/app'

vi.mock('@/api/app', () => ({
  createDebugSession: vi.fn().mockResolvedValue('0198f2c1-1111-7000-8000-000000000001'),
}))

vi.mock('@/api/agentChat', () => ({
  createAppChatAgent: vi.fn(() => ({})),
  runAppChat: vi.fn().mockResolvedValue(undefined),
  abortAppChat: vi.fn(),
}))

describe('AppDebugChat（调试对话面板）', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(createDebugSession).mockResolvedValue('0198f2c1-1111-7000-8000-000000000001')
  })

  it('进入即创建调试会话并提示对话不保存', async () => {
    render(
      <MemoryRouter>
        <AppDebugChat appId="a1" />
      </MemoryRouter>,
    )

    expect(screen.getByText(/对话不保存/)).toBeTruthy()
    await waitFor(() => expect(createDebugSession).toHaveBeenCalledWith('a1'))
  })
})

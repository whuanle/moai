import { describe, expect, it, vi } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/react'
import '@/i18n'
import { Chat, type ChatMessage } from '../'

const messages: ChatMessage[] = [
  { id: '1', role: 'user', content: '你好' },
  { id: '2', role: 'assistant', content: '你好，有什么可以帮你？' },
]

describe('Chat', () => {
  it('renders messages and triggers send', () => {
    const onSend = vi.fn()
    render(<Chat messages={messages} inputValue="hi" onSend={onSend} onInputChange={() => {}} />)
    expect(screen.getByText('你好')).toBeInTheDocument()
    expect(screen.getByText('你好，有什么可以帮你？')).toBeInTheDocument()
    fireEvent.click(screen.getByText('发送'))
    expect(onSend).toHaveBeenCalled()
  })
})

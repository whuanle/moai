import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Page, Chat, type ChatMessage } from '@/design-system'

export function ChatTemplate() {
  const { t } = useTranslation()
  const [inputValue, setInputValue] = useState('')
  const [messages, setMessages] = useState<ChatMessage[]>([])

  const handleSend = () => {
    const text = inputValue.trim()
    if (!text) return
    setMessages((prev) => [
      ...prev,
      { id: String(prev.length + 1), role: 'user', content: text },
    ])
    setInputValue('')
  }

  return (
    <Page title={t('ds.chat.title')} subtitle={t('ds.chat.subtitle')}>
      <Chat
        messages={messages}
        inputValue={inputValue}
        onInputChange={setInputValue}
        onSend={handleSend}
        height={600}
        empty={t('ds.chat.empty')}
      />
    </Page>
  )
}

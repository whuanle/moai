import { Button, ConfigProvider, Input } from 'antd'
import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { brandColors, neutralColors, radius, spacing } from '@/design-system/theme'

export interface ChatMessage {
  id: string
  role: 'user' | 'assistant' | 'system'
  content: string
  time?: string
}

export interface ChatProps {
  messages: ChatMessage[]
  inputValue?: string
  onInputChange?: (value: string) => void
  onSend?: () => void
  sending?: boolean
  empty?: ReactNode
  height?: number | string
}

const BUBBLE_COLORS = {
  user: brandColors.primary,
  other: neutralColors.background,
}

export function Chat({ messages, inputValue = '', onInputChange, onSend, sending, empty, height = 480 }: ChatProps) {
  const { t } = useTranslation()

  return (
    <div
      style={{
        display: 'flex',
        flexDirection: 'column',
        height,
        border: `1px solid ${neutralColors.border}`,
        borderRadius: radius.default,
      }}
    >
      <div style={{ flex: 1, overflowY: 'auto', padding: spacing.lg }}>
        {messages.length === 0 ? (
          empty ?? (
            <div style={{ textAlign: 'center', color: neutralColors.textTertiary, padding: spacing.xl }}>
              暂无消息
            </div>
          )
        ) : (
          messages.map((m) => (
            <div
              key={m.id}
              style={{
                display: 'flex',
                justifyContent: m.role === 'user' ? 'flex-end' : 'flex-start',
                marginBottom: spacing.md,
              }}
            >
              <div
                style={{
                  maxWidth: '75%',
                  padding: `${spacing.xs}px ${spacing.sm}px`,
                  borderRadius: radius.default,
                  background: m.role === 'user' ? BUBBLE_COLORS.user : BUBBLE_COLORS.other,
                  color: m.role === 'user' ? '#fff' : 'inherit',
                  whiteSpace: 'pre-wrap',
                }}
              >
                {m.content}
              </div>
            </div>
          ))
        )}
      </div>
      <ConfigProvider button={{ autoInsertSpace: false }}>
        <div
          style={{
            padding: spacing.md,
            borderTop: `1px solid ${neutralColors.border}`,
            display: 'flex',
            gap: spacing.sm,
          }}
        >
          <Input.TextArea
            autoSize={{ minRows: 1, maxRows: 4 }}
            value={inputValue}
            onChange={(e) => onInputChange?.(e.target.value)}
            placeholder={t('ds.chat.placeholder')}
          />
          <Button type="primary" onClick={onSend} loading={sending} style={{ height: 'auto' }}>
            {t('ds.chat.send')}
          </Button>
        </div>
      </ConfigProvider>
    </div>
  )
}

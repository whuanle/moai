import { CopyOutlined, RobotFilled, ThunderboltFilled } from '@ant-design/icons'
import type { CSSProperties } from 'react'
import { Button, Tooltip } from 'antd'
import { useTranslation } from 'react-i18next'
import ReactMarkdown from 'react-markdown'
import remarkGfm from 'remark-gfm'

export interface DisplayMessage {
  id: string
  role: 'user' | 'assistant'
  content: string
  toolCalls?: string[]
}

export interface ChatMessageListProps {
  messages: DisplayMessage[]
  sending: boolean
  appAvatar?: string
  userName: string
  userAvatar?: string
  onCopy: (text: string) => void
  emptyState?: React.ReactNode
  style?: CSSProperties
}

/**
 * 对话消息流（用户/助手气泡、工具调用标签、Markdown 流式渲染）。
 * 供正式对话页 AppChat 与调试面板 AppDebugChat 复用；样式依赖全局 app-chat.css。
 */
export function ChatMessageList({
  messages,
  sending,
  appAvatar,
  userName,
  userAvatar,
  onCopy,
  emptyState,
  style,
}: ChatMessageListProps) {
  const { t } = useTranslation()

  if (messages.length === 0) {
    return <>{emptyState ?? null}</>
  }

  return (
    <div className="moai-chat__stream" style={style}>
      {messages.map((m, index) => {
        const isLast = index === messages.length - 1
        const streaming = sending && isLast && m.role === 'assistant'
        return (
          <div key={m.id} className={`moai-chat__row moai-chat__row--${m.role}`}>
            {m.role === 'assistant' ? (
              <div className="moai-chat__avatar moai-chat__avatar--ai">
                {appAvatar ? <img src={appAvatar} alt="" style={{ width: 34, height: 34, objectFit: 'cover' }} /> : <RobotFilled />}
              </div>
            ) : (
              <div className="moai-chat__avatar moai-chat__avatar--user">
                {userAvatar ? <img src={userAvatar} alt="" style={{ width: 34, height: 34, objectFit: 'cover' }} /> : userName.slice(0, 1).toUpperCase()}
              </div>
            )}

            <div className="moai-chat__msg">
              {m.role === 'assistant' ? (
                <>
                  {(m.toolCalls?.length ?? 0) > 0 && (
                    <div className="moai-chat__toolbar">
                      {m.toolCalls!.map((name, i) => (
                        <span key={`${name}-${i}`} className="moai-chat__tool-chip">
                          <ThunderboltFilled />
                          {name}
                        </span>
                      ))}
                    </div>
                  )}
                  <div className="moai-chat__assistant">
                    {m.content ? (
                      <div className="moai-chat__markdown">
                        <ReactMarkdown remarkPlugins={[remarkGfm]}>{m.content}</ReactMarkdown>
                      </div>
                    ) : streaming ? (
                      <span className="moai-chat__typing">
                        <span />
                        <span />
                        <span />
                      </span>
                    ) : null}
                    {streaming && m.content && <span className="moai-chat__caret" />}
                  </div>
                  {m.content && !streaming && (
                    <div className="moai-chat__actions">
                      <Tooltip title={t('appChat.copy')}>
                        <Button type="text" size="small" icon={<CopyOutlined />} onClick={() => onCopy(m.content)} />
                      </Tooltip>
                    </div>
                  )}
                </>
              ) : (
                <div className="moai-chat__bubble">{m.content}</div>
              )}
            </div>
          </div>
        )
      })}
    </div>
  )
}

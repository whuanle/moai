import { CaretRightOutlined, CopyOutlined, RobotFilled } from '@ant-design/icons'
import { useState, type CSSProperties, type ReactNode } from 'react'
import { Button, Tooltip } from 'antd'
import { useTranslation } from 'react-i18next'
import ReactMarkdown from 'react-markdown'
import remarkGfm from 'remark-gfm'
import { attachmentImageSrc, getAttachmentFileIcon, parseAttachmentMessage, type ParsedAttachment } from './attachment'
import { ToolCallCard, type ToolCallDisplay } from './ToolCallCard'

export type { ToolCallDisplay, ToolCallStatus } from './ToolCallCard'

export interface DisplayMessage {
  id: string
  role: 'user' | 'assistant'
  content: string
  toolCalls?: ToolCallDisplay[]
}

export interface ChatMessageListProps {
  messages: DisplayMessage[]
  sending: boolean
  appAvatar?: string
  userName: string
  userAvatar?: string
  onCopy: (text: string) => void
  emptyState?: ReactNode
  /** 审批卡「批准执行」回调（审批模式下挂起的工具调用） */
  onToolApprove?: (messageId: string, toolCall: ToolCallDisplay) => void
  /** 审批卡「拒绝」回调 */
  onToolReject?: (messageId: string, toolCall: ToolCallDisplay) => void
  style?: CSSProperties
}

/**
 * 用户消息内的附件 chip：文档附件可展开查看提取文本，图片附件点击打开原图.
 */
function AttachmentChips({ attachments }: { attachments: ParsedAttachment[] }) {
  const { t } = useTranslation()
  const [expanded, setExpanded] = useState<string | null>(null)
  if (attachments.length === 0) return null
  return (
    <div className="moai-chat__bubble-attachments">
      {attachments.map((att, index) => {
        const key = `${att.name}#${index}`
        // 图片附件：新格式内容为裸 URL、历史格式为 [图片附件](url)，均展示缩略图；文档按扩展名取类型图标
        const imageSrc = attachmentImageSrc(att.content)
        const FileIcon = getAttachmentFileIcon(att.name)
        return (
          <div key={key} className="moai-chat__bubble-attachment">
            <div className="moai-chat__bubble-attachment-chip">
              {imageSrc ? (
                <img src={imageSrc} alt="" className="moai-chat__bubble-attachment-thumb" />
              ) : (
                <FileIcon />
              )}
              {imageSrc ? (
                <a href={imageSrc} target="_blank" rel="noreferrer" className="moai-chat__bubble-attachment-name">
                  {att.name}
                </a>
              ) : (
                <button
                  type="button"
                  className="moai-chat__bubble-attachment-name"
                  onClick={() => setExpanded(expanded === key ? null : key)}
                >
                  {att.name}
                </button>
              )}
              {!imageSrc && (
                <button
                  type="button"
                  className="moai-chat__bubble-attachment-toggle"
                  onClick={() => setExpanded(expanded === key ? null : key)}
                  aria-label={t('appChat.attachmentViewContent')}
                >
                  <CaretRightOutlined className={expanded === key ? 'is-open' : ''} />
                </button>
              )}
            </div>
            {expanded === key && att.content && (
              <pre className="moai-chat__bubble-attachment-content">{att.content}</pre>
            )}
          </div>
        )
      })}
    </div>
  )
}

/**
 * 对话消息流（用户/助手气泡、工具调用卡片、Markdown 流式渲染）。
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
  onToolApprove,
  onToolReject,
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
                    <div className="moai-chat__tools">
                      {m.toolCalls!.map((toolCall) => (
                        <ToolCallCard
                          key={toolCall.id}
                          toolCall={toolCall}
                          onApprove={onToolApprove ? () => onToolApprove(m.id, toolCall) : undefined}
                          onReject={onToolReject ? () => onToolReject(m.id, toolCall) : undefined}
                        />
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
                (() => {
                  const parsed = parseAttachmentMessage(m.content)
                  return (
                    <>
                      {parsed.text && <div className="moai-chat__bubble">{parsed.text}</div>}
                      <AttachmentChips attachments={parsed.attachments} />
                    </>
                  )
                })()
              )}
            </div>
          </div>
        )
      })}
    </div>
  )
}

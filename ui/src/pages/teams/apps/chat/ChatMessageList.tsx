import { CaretRightOutlined, CopyOutlined, RobotFilled, ThunderboltFilled } from '@ant-design/icons'
import { useMemo, useState, type CSSProperties, type ReactNode } from 'react'
import { Button, Tooltip } from 'antd'
import { useTranslation } from 'react-i18next'
import ReactMarkdown from 'react-markdown'
import remarkGfm from 'remark-gfm'
import { attachmentImageSrc, getAttachmentFileIcon, parseAttachmentMessage, type ParsedAttachment } from './attachment'
import { ToolCallCard, type ToolCallDisplay, type ToolCallStatus } from './ToolCallCard'

export type { ToolCallDisplay, ToolCallStatus } from './ToolCallCard'

/** 仍在推进的工具调用状态：固定展示在内容底部（随时可见），其余状态归入头部折叠列表 */
const ACTIVE_TOOL_STATUSES: ReadonlySet<ToolCallStatus> = new Set(['running', 'awaiting', 'approved'])

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
 * 已完成工具调用的折叠列表：单条直接展示明细，多条默认收缩为一行摘要（点击展开/收起）.
 */
function ToolCallGroup({ calls }: { calls: ToolCallDisplay[] }) {
  const { t } = useTranslation()
  const [open, setOpen] = useState(false)
  if (calls.length === 1) {
    return <ToolCallCard toolCall={calls[0]} />
  }
  return (
    <div className="moai-chat__tool-group">
      <button type="button" className="moai-chat__tool-group-head" onClick={() => setOpen((v) => !v)} aria-expanded={open}>
        <ThunderboltFilled className="moai-chat__tool-status-icon is-done" />
        <span className="moai-chat__tool-group-title">{t('appChat.toolCallCount', { count: calls.length })}</span>
        <CaretRightOutlined className={`moai-chat__tool-group-caret${open ? ' is-open' : ''}`} />
      </button>
      {open && (
        <div className="moai-chat__tool-group-body">
          {calls.map((toolCall) => (
            <ToolCallCard key={toolCall.id} toolCall={toolCall} />
          ))}
        </div>
      )}
    </div>
  )
}

/**
 * 对话消息流（用户/助手消息、工具调用行、Markdown 流式渲染）。
 * 一次回答的多个回合（用户提问后 AI 连续产出的多条助手消息）分组渲染：共用一个头像，
 * 组内不加水平分隔线，分隔线只画在分组之间；供正式对话页 AppChat 与调试面板复用，样式依赖全局 app-chat.css。
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

  // 相邻助手消息合并为一个回答分组；用户消息自成一组
  const groups = useMemo(() => {
    const result: DisplayMessage[][] = []
    for (const m of messages) {
      const last = result[result.length - 1]
      if (m.role === 'assistant' && last && last[0].role === 'assistant') {
        last.push(m)
      } else {
        result.push([m])
      }
    }
    return result
  }, [messages])

  if (messages.length === 0) {
    return <>{emptyState ?? null}</>
  }

  const lastMessageId = messages[messages.length - 1]?.id

  return (
    <div className="moai-chat__stream" style={style}>
      {groups.map((group) => {
        const isUser = group[0].role === 'user'
        return (
          <div key={group[0].id} className={`moai-chat__row moai-chat__row--${group[0].role}`}>
            {isUser ? (
              <div className="moai-chat__avatar moai-chat__avatar--user">
                {userAvatar ? <img src={userAvatar} alt="" style={{ width: 34, height: 34, objectFit: 'cover' }} /> : userName.slice(0, 1).toUpperCase()}
              </div>
            ) : (
              <div className="moai-chat__avatar moai-chat__avatar--ai">
                {appAvatar ? <img src={appAvatar} alt="" style={{ width: 34, height: 34, objectFit: 'cover' }} /> : <RobotFilled />}
              </div>
            )}

            <div className="moai-chat__msg">
              {isUser ? (
                (() => {
                  const parsed = parseAttachmentMessage(group[0].content)
                  return (
                    <>
                      {parsed.text && <div className="moai-chat__bubble">{parsed.text}</div>}
                      <AttachmentChips attachments={parsed.attachments} />
                    </>
                  )
                })()
              ) : (
                group.map((m) => {
                  const streaming = sending && m.id === lastMessageId
                  const toolCalls = m.toolCalls ?? []
                  const completed = toolCalls.filter((tc) => !ACTIVE_TOOL_STATUSES.has(tc.status))
                  const active = toolCalls.filter((tc) => ACTIVE_TOOL_STATUSES.has(tc.status))
                  return (
                    <div key={m.id} className="moai-chat__turn">
                      {completed.length > 0 && <ToolCallGroup calls={completed} />}
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
                      {active.length > 0 && (
                        <div className="moai-chat__tools">
                          {active.map((toolCall) => (
                            <ToolCallCard
                              key={toolCall.id}
                              toolCall={toolCall}
                              onApprove={onToolApprove ? () => onToolApprove(m.id, toolCall) : undefined}
                              onReject={onToolReject ? () => onToolReject(m.id, toolCall) : undefined}
                            />
                          ))}
                        </div>
                      )}
                      {m.content && !streaming && (
                        <div className="moai-chat__actions">
                          <Tooltip title={t('appChat.copy')}>
                            <Button type="text" size="small" icon={<CopyOutlined />} onClick={() => onCopy(m.content)} />
                          </Tooltip>
                        </div>
                      )}
                    </div>
                  )
                })
              )}
            </div>
          </div>
        )
      })}
    </div>
  )
}

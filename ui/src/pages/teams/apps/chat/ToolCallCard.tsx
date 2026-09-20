import { useMemo, useState } from 'react'
import {
  CheckCircleFilled,
  ClockCircleOutlined,
  CloseCircleFilled,
  DownOutlined,
  SafetyCertificateFilled,
  ThunderboltFilled,
  UpOutlined,
} from '@ant-design/icons'
import { Button } from 'antd'
import { useTranslation } from 'react-i18next'

/** 工具调用在对话流中的展示状态 */
export type ToolCallStatus = 'running' | 'awaiting' | 'approved' | 'rejected' | 'done' | 'timeout'

/** 对话消息内嵌的工具调用记录：name 为真实工具名（call_tool 的内层 toolName） */
export interface ToolCallDisplay {
  id: string
  name: string
  argsJson?: string
  status: ToolCallStatus
}

export interface ToolCallCardProps {
  toolCall: ToolCallDisplay
  onApprove?: (toolCall: ToolCallDisplay) => void
  onReject?: (toolCall: ToolCallDisplay) => void
}

const STATUS_ICON_CLASS: Record<ToolCallStatus, string> = {
  running: 'moai-chat__tool-status-icon is-running',
  awaiting: 'moai-chat__tool-status-icon is-awaiting',
  approved: 'moai-chat__tool-status-icon is-approved',
  rejected: 'moai-chat__tool-status-icon is-rejected',
  done: 'moai-chat__tool-status-icon is-done',
  timeout: 'moai-chat__tool-status-icon is-timeout',
}

/**
 * 工具调用卡片：展示工具名/参数与执行状态；审批模式下挂起等待时提供「批准/拒绝」操作，
 * 状态随决策与流式进展流转（等待审批 → 已批准·执行中 → 已完成 / 已拒绝 / 超时未执行）。
 */
export function ToolCallCard({ toolCall, onApprove, onReject }: ToolCallCardProps) {
  const { t } = useTranslation()
  const [argsOpen, setArgsOpen] = useState(false)

  const prettyArgs = useMemo(() => {
    if (!toolCall.argsJson) return ''
    try {
      return JSON.stringify(JSON.parse(toolCall.argsJson), null, 2)
    } catch {
      return toolCall.argsJson
    }
  }, [toolCall.argsJson])

  return (
    <div className={`moai-chat__tool-card is-${toolCall.status}`}>
      <div className="moai-chat__tool-card-head">
        {toolCall.status === 'awaiting' ? (
          <SafetyCertificateFilled className={STATUS_ICON_CLASS[toolCall.status]} />
        ) : (
          <ThunderboltFilled className={STATUS_ICON_CLASS[toolCall.status]} />
        )}
        <span className="moai-chat__tool-card-name">{toolCall.name}</span>
        <span className={`moai-chat__tool-card-status is-${toolCall.status}`}>
          {toolCall.status === 'running' && <ClockCircleOutlined spin />}
          {toolCall.status === 'awaiting' && <SafetyCertificateFilled />}
          {toolCall.status === 'approved' && <ClockCircleOutlined spin />}
          {toolCall.status === 'done' && <CheckCircleFilled />}
          {toolCall.status === 'rejected' && <CloseCircleFilled />}
          {toolCall.status === 'timeout' && <ClockCircleOutlined />}
          {t(`appChat.toolStatus.${toolCall.status}`)}
        </span>
        {prettyArgs && (
          <button
            type="button"
            className="moai-chat__tool-card-toggle"
            onClick={() => setArgsOpen((v) => !v)}
            aria-label={t('appChat.toolArgs')}
          >
            {argsOpen ? <UpOutlined /> : <DownOutlined />}
            {t('appChat.toolArgs')}
          </button>
        )}
      </div>
      {argsOpen && prettyArgs && <pre className="moai-chat__tool-card-args">{prettyArgs}</pre>}
      {toolCall.status === 'awaiting' && (
        <div className="moai-chat__tool-card-actions">
          <Button type="primary" size="small" onClick={() => onApprove?.(toolCall)}>
            {t('appChat.toolApprove')}
          </Button>
          <Button danger size="small" onClick={() => onReject?.(toolCall)}>
            {t('appChat.toolReject')}
          </Button>
        </div>
      )}
    </div>
  )
}

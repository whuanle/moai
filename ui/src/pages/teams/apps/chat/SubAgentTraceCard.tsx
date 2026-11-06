import { useState, type ReactNode } from 'react'
import { CheckCircleFilled, CaretRightOutlined, ClockCircleOutlined, CloseCircleFilled, ThunderboltFilled } from '@ant-design/icons'
import { Avatar, Button } from 'antd'
import { useTranslation } from 'react-i18next'
import { taskFromToolCall, type SubAgentTraceState } from './subAgentTrace'
import type { ToolCallDisplay } from './ToolCallCard'

/** 子智能体卡头部状态图标（运行中/完成/超时/失败） */
const TRACE_STATUS_ICON: Record<SubAgentTraceState['status'], ReactNode> = {
  running: <ClockCircleOutlined spin />,
  done: <CheckCircleFilled />,
  timeout: <ClockCircleOutlined />,
  failed: <CloseCircleFilled />,
}

/** 耗时展示：秒级一位小数 */
function formatElapsed(ms?: number): string | null {
  if (ms == null || ms <= 0) return null
  return `${(ms / 1000).toFixed(1)}s`
}

export interface SubAgentTraceCardProps {
  toolCall: ToolCallDisplay
  trace: SubAgentTraceState
  /** 子智能体头像地址（页面层按 key 从 userconfig 解析，缺省回退首字图标） */
  avatar?: string
  onApprove?: (toolCall: ToolCallDisplay) => void
  onReject?: (toolCall: ToolCallDisplay) => void
}

/**
 * 子智能体派发嵌套活动卡（codex 风格）：头部为子智能体头像/名称/状态/耗时，展开区实时展示
 * 委派任务、子代理正文流式输出与嵌套工具步骤；完成后折叠为一行摘要（含结论），可展开回看.
 * 实时数据来自 moai.subagent 自定义事件（不持久化）；历史回放仅任务 + 最终结论（结果级回放）.
 */
export function SubAgentTraceCard({ toolCall, trace, avatar, onApprove, onReject }: SubAgentTraceCardProps) {
  const { t } = useTranslation()
  // 运行中默认展开，完成后折叠为一行摘要
  const [open, setOpen] = useState(trace.status === 'running')
  const task = taskFromToolCall(toolCall)
  const elapsed = formatElapsed(trace.elapsedMilliseconds)
  const pendingDispatch = trace.status === 'running' && !trace.text && trace.steps.length === 0
  const collapsedSummary = !open && trace.finalText ? trace.finalText.split('\n')[0] : null

  return (
    <div className={`moai-chat__tool-card moai-chat__subagent-card is-${trace.status}`}>
      <button
        type="button"
        className="moai-chat__subagent-card-head"
        onClick={() => setOpen((v) => !v)}
        aria-expanded={open}
      >
        <Avatar shape="square" size={18} src={avatar || undefined} className="moai-chat__subagent-avatar">
          {(trace.agentName || '?').slice(0, 1).toUpperCase()}
        </Avatar>
        <span className="moai-chat__tool-card-name">{trace.agentName}</span>
        <span className="moai-chat__subagent-badge">{t('appChat.subAgentTrace.badge')}</span>
        <span className={`moai-chat__tool-card-status is-${trace.status}`}>
          {TRACE_STATUS_ICON[trace.status]}
          {pendingDispatch
            ? t('appChat.subAgentTrace.pending')
            : t(`appChat.subAgentTrace.status.${trace.status}`)}
        </span>
        {elapsed && <span className="moai-chat__subagent-elapsed">{elapsed}</span>}
        <CaretRightOutlined className={`moai-chat__tool-group-caret${open ? ' is-open' : ''}`} />
      </button>
      {open && (
        <div className="moai-chat__subagent-body">
          {task && (
            <div className="moai-chat__subagent-task">
              <span className="moai-chat__subagent-label">{t('appChat.subAgentTrace.task')}</span>
              <span>{task}</span>
            </div>
          )}
          {trace.steps.length > 0 && (
            <div className="moai-chat__subagent-steps">
              {trace.steps.map((step) => (
                <div key={step.id} className="moai-chat__subagent-step">
                  <div className="moai-chat__subagent-step-head">
                    <ThunderboltFilled className={step.result == null ? 'moai-chat__tool-status-icon is-running' : 'moai-chat__tool-status-icon is-done'} />
                    <span className="moai-chat__subagent-step-name">{step.toolName}</span>
                  </div>
                  {step.result != null && <div className="moai-chat__subagent-step-result">{step.result}</div>}
                </div>
              ))}
            </div>
          )}
          {trace.text && <div className="moai-chat__subagent-text">{trace.text}</div>}
          {trace.status !== 'running' && trace.finalText && (
            <div className="moai-chat__subagent-final">
              <span className="moai-chat__subagent-label">{t('appChat.subAgentTrace.conclusion')}</span>
              <span>{trace.finalText}</span>
            </div>
          )}
          {trace.status === 'failed' && !trace.finalText && (
            <div className="moai-chat__subagent-final">{t('appChat.subAgentTrace.failedEmpty')}</div>
          )}
        </div>
      )}
      {collapsedSummary && <div className="moai-chat__subagent-summary">{collapsedSummary}</div>}
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

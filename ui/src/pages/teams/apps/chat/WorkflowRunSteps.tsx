import { useState } from 'react'
import {
  CheckCircleFilled,
  ClockCircleOutlined,
  CloseCircleFilled,
  DownOutlined,
  LoadingOutlined,
  MinusCircleOutlined,
  UpOutlined,
} from '@ant-design/icons'
import { Button, Tooltip } from 'antd'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router'
import type { WorkflowRunStep } from './useWorkflowRunSteps'

/**
 * 流程应用对话的执行过程步骤条：节点名 + 状态图标 + 耗时，失败显示错误信息；
 * 运行中实时更新（AG-UI CustomEvent 推送），结束后可折叠并提供运行详情入口.
 */
export function WorkflowRunSteps({
  steps,
  running,
  error,
  instanceId,
  teamId,
  appId,
}: {
  steps: WorkflowRunStep[]
  running: boolean
  error?: string
  instanceId?: string
  teamId: number
  appId: string
}) {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [expanded, setExpanded] = useState(true)

  if (steps.length === 0 && !error) return null

  const finishedCount = steps.filter((s) => s.state === 'completed' || s.state === 'skipped').length
  const failed = steps.some((s) => s.state === 'failed') || Boolean(error)

  return (
    <div className={`moai-chat__wf-steps${failed ? ' is-failed' : ''}`} role="status">
      <button
        type="button"
        className="moai-chat__wf-steps-head"
        onClick={() => setExpanded((prev) => !prev)}
        aria-expanded={expanded}
      >
        <span className="moai-chat__wf-steps-title">
          {running ? <LoadingOutlined spin /> : failed ? <CloseCircleFilled className="is-failed-icon" /> : <CheckCircleFilled className="is-done-icon" />}
          <span>{t('appChat.workflowSteps')}</span>
          {steps.length > 0 && (
            <span className="moai-chat__wf-steps-count">
              {finishedCount}/{steps.length}
            </span>
          )}
        </span>
        {expanded ? <DownOutlined /> : <UpOutlined />}
      </button>
      {expanded && (
        <div className="moai-chat__wf-steps-body">
          {steps.map((step) => (
            <div key={step.nodeKey} className={`moai-chat__wf-step is-${step.state}`}>
              <StepIcon state={step.state} />
              <span className="moai-chat__wf-step-name">{step.nodeName}</span>
              {step.state === 'running' && <span className="moai-chat__wf-step-hint">{t('appChat.workflowStepRunning')}</span>}
              {typeof step.elapsedMilliseconds === 'number' && step.elapsedMilliseconds > 0 && (
                <span className="moai-chat__wf-step-elapsed">{(step.elapsedMilliseconds / 1000).toFixed(1)}s</span>
              )}
              {step.state === 'failed' && step.errorMessage && (
                <Tooltip title={step.errorMessage}>
                  <span className="moai-chat__wf-step-error">{step.errorMessage}</span>
                </Tooltip>
              )}
            </div>
          ))}
          {error && <div className="moai-chat__wf-step-error-line">{error}</div>}
          {!running && instanceId && (
            <Button
              type="link"
              size="small"
              className="moai-chat__wf-steps-link"
              onClick={() => navigate(`/team/${teamId}/app/${appId}/runs?instanceId=${instanceId}`)}
            >
              {t('appChat.workflowViewRun')}
            </Button>
          )}
        </div>
      )}
    </div>
  )
}

function StepIcon({ state }: { state: WorkflowRunStep['state'] }) {
  switch (state) {
    case 'running':
      return <LoadingOutlined spin className="is-running-icon" />
    case 'completed':
      return <CheckCircleFilled className="is-done-icon" />
    case 'failed':
      return <CloseCircleFilled className="is-failed-icon" />
    case 'skipped':
      return <MinusCircleOutlined className="is-skipped-icon" />
    default:
      return <ClockCircleOutlined className="is-pending-icon" />
  }
}

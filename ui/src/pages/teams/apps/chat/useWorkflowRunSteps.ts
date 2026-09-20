import { useCallback, useState } from 'react'
import type { WorkflowChatEventPayload } from '@/api/agentChat'

/** 流程执行过程步骤（与后端 NodeStateChangedEvent 映射契约一致） */
export interface WorkflowRunStep {
  nodeKey: string
  nodeName: string
  nodeType: string
  state: NonNullable<WorkflowChatEventPayload['nodeState']>
  errorMessage?: string | null
  elapsedMilliseconds?: number | null
}

/**
 * 流程应用对话的执行过程状态：消费 AG-UI CustomEvent("moai.workflow") 事件流，
 * 维护节点步骤列表 / 实例 id / 运行态 / 挂起错误，供 WorkflowRunSteps 展示.
 */
export function useWorkflowRunSteps() {
  const [steps, setSteps] = useState<WorkflowRunStep[]>([])
  const [instanceId, setInstanceId] = useState('')
  const [running, setRunning] = useState(false)
  const [error, setError] = useState('')

  const reset = useCallback(() => {
    setSteps([])
    setInstanceId('')
    setError('')
    setRunning(true)
  }, [])

  const finish = useCallback(() => setRunning(false), [])

  const handleEvent = useCallback((evt: WorkflowChatEventPayload) => {
    switch (evt.event) {
      case 'started':
        setInstanceId(evt.instanceId ?? '')
        break
      case 'node': {
        if (!evt.nodeKey) return
        const step: WorkflowRunStep = {
          nodeKey: evt.nodeKey,
          nodeName: evt.nodeName || evt.nodeKey,
          nodeType: evt.nodeType ?? '',
          state: evt.nodeState ?? 'pending',
          errorMessage: evt.errorMessage,
          elapsedMilliseconds: evt.elapsedMilliseconds,
        }
        setSteps((prev) => {
          const index = prev.findIndex((s) => s.nodeKey === step.nodeKey)
          if (index < 0) return [...prev, step]
          const next = [...prev]
          next[index] = step
          return next
        })
        break
      }
      case 'suspended':
        setError(evt.message || evt.errorMessage || '')
        setRunning(false)
        break
      case 'completed':
        setRunning(false)
        break
      default:
        break
    }
  }, [])

  return { steps, instanceId, running, error, reset, finish, handleEvent }
}

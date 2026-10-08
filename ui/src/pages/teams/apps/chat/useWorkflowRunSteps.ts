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
 * 流程执行过程状态：消费 AG-UI CustomEvent("moai.workflow") 事件流，
 * 维护节点步骤列表 / 实例 id / 运行态 / 挂起错误，供 WorkflowRunSteps 展示.
 * 以纯函数维护（create/apply/finish），对话页按会话 id 分桶存储以支持多会话并行。
 */
export interface WorkflowRunState {
  steps: WorkflowRunStep[]
  instanceId: string
  running: boolean
  error: string
}

export function createWorkflowRunState(running = false): WorkflowRunState {
  return { steps: [], instanceId: '', running, error: '' }
}

export function applyWorkflowEvent(state: WorkflowRunState, evt: WorkflowChatEventPayload): WorkflowRunState {
  switch (evt.event) {
    case 'started':
      return { ...state, instanceId: evt.instanceId ?? '' }
    case 'node': {
      if (!evt.nodeKey) return state
      const step: WorkflowRunStep = {
        nodeKey: evt.nodeKey,
        nodeName: evt.nodeName || evt.nodeKey,
        nodeType: evt.nodeType ?? '',
        state: evt.nodeState ?? 'pending',
        errorMessage: evt.errorMessage,
        elapsedMilliseconds: evt.elapsedMilliseconds,
      }
      const index = state.steps.findIndex((s) => s.nodeKey === step.nodeKey)
      if (index < 0) return { ...state, steps: [...state.steps, step] }
      const steps = [...state.steps]
      steps[index] = step
      return { ...state, steps }
    }
    case 'suspended':
      return { ...state, error: evt.message || evt.errorMessage || '', running: false }
    case 'completed':
      return { ...state, running: false }
    default:
      return state
  }
}

export function finishWorkflowRun(state: WorkflowRunState): WorkflowRunState {
  return state.running ? { ...state, running: false } : state
}

/** 单会话流程执行状态 hook（调试面板等单会话场景使用） */
export function useWorkflowRunSteps() {
  const [state, setState] = useState<WorkflowRunState>(() => createWorkflowRunState())

  const reset = useCallback(() => setState(createWorkflowRunState(true)), [])

  const finish = useCallback(() => setState(finishWorkflowRun), [])

  const handleEvent = useCallback((evt: WorkflowChatEventPayload) => {
    setState((prev) => applyWorkflowEvent(prev, evt))
  }, [])

  return { steps: state.steps, instanceId: state.instanceId, running: state.running, error: state.error, reset, finish, handleEvent }
}

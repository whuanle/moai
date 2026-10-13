import { getApiClient } from '@/api/kiota'
import type { TeamTimerItem, TeamTimerExecutionItem } from '@/api/client/models'

export type { TeamTimerItem, TeamTimerExecutionItem }

/** 定时器执行状态（对齐后端 TimerExecutionStatus 枚举） */
export const TIMER_STATUS = {
  Pending: 'pending',
  Running: 'running',
  Success: 'success',
  Failed: 'failed',
  Retrying: 'retrying',
} as const

/** 触发方式（对齐后端 TimerTriggerType 枚举） */
export const TIMER_TRIGGER = {
  Scheduled: 'scheduled',
  Manual: 'manual',
} as const

/** 未到达终态的执行状态（前端据此轮询刷新） */
export function isTimerStatusActive(status?: string | null): boolean {
  return status === TIMER_STATUS.Pending || status === TIMER_STATUS.Running || status === TIMER_STATUS.Retrying
}

export interface TeamTimersResult {
  totalCount: number
  items: TeamTimerItem[]
}

export interface TimerExecutionsResult {
  totalCount: number
  items: TeamTimerExecutionItem[]
}

export interface TimerPayload {
  name: string
  description?: string
  appId: string
  cronExpression: string
  inputContent: string
  isEnabled: boolean
  maxRetryCount: number
  retryIntervalSeconds: number
}

/** 查询团队定时器列表 */
export async function getTeamTimers(teamId: number, filters?: { pageNo?: number; pageSize?: number; keyword?: string }): Promise<TeamTimersResult> {
  const client = getApiClient()
  const res = await client.api.timer.list.get({
    queryParameters: {
      teamId: String(teamId),
      pageNo: filters?.pageNo ?? 1,
      pageSize: filters?.pageSize ?? 20,
      keyword: filters?.keyword || undefined,
    },
  })
  return { totalCount: res?.totalCount ?? 0, items: res?.items ?? [] }
}

/** 创建定时器，返回定时器 id */
export async function createTeamTimer(teamId: number, payload: TimerPayload): Promise<string> {
  const client = getApiClient()
  const res = await client.api.timer.post({
    teamId: String(teamId),
    name: payload.name,
    description: payload.description || undefined,
    appId: payload.appId,
    cronExpression: payload.cronExpression,
    inputContent: payload.inputContent,
    isEnabled: payload.isEnabled,
    maxRetryCount: payload.maxRetryCount,
    retryIntervalSeconds: payload.retryIntervalSeconds,
  })
  return String(res?.value ?? '')
}

/** 更新定时器（全字段覆盖） */
export async function updateTeamTimer(timerId: string, payload: TimerPayload): Promise<void> {
  const client = getApiClient()
  await client.api.timer.byId(String(timerId)).put({
    name: payload.name,
    description: payload.description || undefined,
    appId: payload.appId,
    cronExpression: payload.cronExpression,
    inputContent: payload.inputContent,
    isEnabled: payload.isEnabled,
    maxRetryCount: payload.maxRetryCount,
    retryIntervalSeconds: payload.retryIntervalSeconds,
  })
}

/** 删除定时器（软删除并移除调度，历史保留） */
export async function deleteTeamTimer(timerId: string): Promise<void> {
  const client = getApiClient()
  await client.api.timer.byId(String(timerId)).delete()
}

/** 立即执行一次，返回执行记录 id */
export async function runTeamTimer(timerId: string): Promise<string> {
  const client = getApiClient()
  const res = await client.api.timer.byId(String(timerId)).run.post()
  return String(res?.value ?? '')
}

/** 查询定时器执行历史（按触发时间倒序，默认页大小 10） */
export async function getTeamTimerExecutions(timerId: string, pageNo = 1, pageSize = 10): Promise<TimerExecutionsResult> {
  const client = getApiClient()
  const res = await client.api.timer.byId(String(timerId)).executions.get({
    queryParameters: { pageNo, pageSize },
  })
  return { totalCount: res?.totalCount ?? 0, items: res?.items ?? [] }
}

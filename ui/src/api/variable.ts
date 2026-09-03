import { getApiClient } from '@/api/kiota'

export interface TeamVariableItem {
  /** 后端 long 序列化为字符串 */
  variableId?: string | number | null
  teamId?: string | number | null
  key?: string | null
  groupName?: string | null
  isSecret?: boolean | null
  /** 私密变量对成员/列表恒为空 */
  value?: string | null
  description?: string | null
  updateTime?: string | null
}

export interface VariablesResult {
  teamId?: string | number | null
  /** 0=Owner 1=Admin 2=Member */
  myRole?: number | null
  items?: TeamVariableItem[] | null
}

export async function getVariables(teamId: number, filters?: { groupName?: string; keyword?: string }): Promise<VariablesResult> {
  const client = getApiClient()
  const res = await client.api.variable.list.get({
    queryParameters: {
      teamId: String(teamId),
      groupName: filters?.groupName || undefined,
      keyword: filters?.keyword || undefined,
    },
  })
  return { teamId: res?.teamId, myRole: res?.myRole, items: res?.items ?? [] }
}

export async function getVariableDetail(variableId: number) {
  const client = getApiClient()
  return client.api.variable.byId(String(variableId)).get()
}

export async function createVariable(payload: {
  teamId: number
  key: string
  groupName?: string
  isSecret: boolean
  value: string
  description?: string
}): Promise<number> {
  const client = getApiClient()
  const res = await client.api.variable.post({
    teamId: String(payload.teamId),
    key: payload.key,
    groupName: payload.groupName,
    isSecret: payload.isSecret,
    value: payload.value,
    description: payload.description,
  })
  return Number(res?.value ?? 0)
}

/** value 为 undefined 表示保持不变（私密变量推荐） */
export async function updateVariable(
  variableId: number,
  payload: { groupName?: string; value?: string; description?: string },
): Promise<void> {
  const client = getApiClient()
  await client.api.variable.byId(String(variableId)).put({
    groupName: payload.groupName ?? null,
    value: payload.value ?? null,
    description: payload.description ?? null,
  })
}

export async function deleteVariable(variableId: number): Promise<void> {
  const client = getApiClient()
  await client.api.variable.byId(String(variableId)).delete()
}

export async function substituteVariables(teamId: number, content: string): Promise<string> {
  const client = getApiClient()
  const res = await client.api.variable.substitute.post({ teamId: String(teamId), content })
  return res?.content ?? ''
}

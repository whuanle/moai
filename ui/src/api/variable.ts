import { getApiClient } from '@/api/kiota'

export interface TeamVariableItem {
  /** 后端 long 序列化为字符串 */
  variableId?: string | number | null
  teamId?: string | number | null
  key?: string | null
  /** 变量名称，空串=未填写 */
  name?: string | null
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

export async function getVariables(teamId: number, filters?: { name?: string; keyword?: string }): Promise<VariablesResult> {
  const client = getApiClient()
  const res = await client.api.variable.list.get({
    queryParameters: {
      teamId: String(teamId),
      name: filters?.name || undefined,
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
  name?: string
  isSecret: boolean
  value: string
  description?: string
}): Promise<number> {
  const client = getApiClient()
  const res = await client.api.variable.post({
    teamId: String(payload.teamId),
    key: payload.key,
    name: payload.name,
    isSecret: payload.isSecret,
    value: payload.value,
    description: payload.description,
  })
  return Number(res?.value ?? 0)
}

/** value 为 undefined 表示保持不变（私密变量推荐）；key/name/description 为 undefined 表示不修改 */
export async function updateVariable(
  variableId: number,
  payload: { key?: string; name?: string; value?: string; description?: string },
): Promise<void> {
  const client = getApiClient()
  await client.api.variable.byId(String(variableId)).put({
    key: payload.key ?? null,
    name: payload.name ?? null,
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

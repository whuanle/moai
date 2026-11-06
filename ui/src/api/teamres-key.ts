import { getApiClient } from '@/api/kiota'
import type { TeamResourceType } from '@/api/client/models'

/** 团队资源 key 项（key 明文回显，仅团队 Admin+ 可见；一个资源一把 key，key 无启停状态） */
export interface TeamResourceKey {
  teamResourceKeyId?: string | null
  teamId?: string | number | null
  resourceType?: TeamResourceType | null
  /** app 行=应用 Guid、wiki 行=wikiId、knowledgeGraph 行=kgId */
  resourceId?: string | null
  /** key 原文（moai-tr- 前缀） */
  key?: string | null
  keyPrefix?: string | null
  createTime?: string | null
  updateTime?: string | null
}

export interface TeamResourceKeysResult {
  teamId?: string | number | null
  /** 0=Member 1=Admin 2=Owner */
  myRole?: number | null
  items?: TeamResourceKey[] | null
}

export interface GeneratedTeamResourceKey {
  teamResourceKeyId?: string | null
  resourceType?: TeamResourceType | null
  /** 资源 id（字符串形态） */
  resourceId?: string | null
  key?: string | null
  keyPrefix?: string | null
  /** 是否新生成（false=已存在原样返回） */
  created?: boolean | null
}

/** 查询团队资源 key 列表（仅团队 Admin+） */
export async function getTeamResourceKeys(teamId: number): Promise<TeamResourceKeysResult> {
  const client = getApiClient()
  const res = await client.api.teamResourceKey.list.get({
    queryParameters: { teamId: String(teamId) },
  })
  return { teamId: res?.teamId, myRole: res?.myRole, items: res?.items ?? [] }
}

/**
 * 生成资源 key（get-or-create：同一资源实例已有 key 原样返回）。
 * 资源未开启对应对外功能（应用 ACP/A2A、知识库/知识图谱 MCP）时 400——开启功能时后端会自动生成，一般无需手动调用。
 */
export async function createTeamResourceKey(
  payload: { teamId: number; resourceType: TeamResourceType; resourceId: string | number },
): Promise<GeneratedTeamResourceKey> {
  const client = getApiClient()
  const res = await client.api.teamResourceKey.post({
    teamId: String(payload.teamId),
    resourceType: payload.resourceType,
    resourceId: String(payload.resourceId),
  })
  return {
    teamResourceKeyId: res?.teamResourceKeyId,
    resourceType: res?.resourceType,
    resourceId: res?.resourceId,
    key: res?.key,
    keyPrefix: res?.keyPrefix,
    created: res?.created,
  }
}

/** 重置资源 key：同表行重新生成 key 值（行 id 不变），旧 key 立即失效；key 不可删除（吊销=重置或关闭资源功能开关） */
export async function resetTeamResourceKey(teamResourceKeyId: string): Promise<string | null | undefined> {
  const client = getApiClient()
  const res = await client.api.teamResourceKey.byId(teamResourceKeyId).reset.post()
  return res?.key
}

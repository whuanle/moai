import { type Guid } from '@microsoft/kiota-abstractions'
import { getApiClient } from '@/api/kiota'

/** 企业微信应用连接列表项（不回显 Secret/Token/EncodingAESKey） */
export interface WeixinWorkAppItemView {
  weixinWorkAppId: string
  teamId: number
  name: string
  description?: string | null
  /** 企业微信企业 ID */
  corpId: string
  /** 企业微信自建应用 AgentId */
  agentId: number
  /** API 地址覆盖，空表示使用官方地址 */
  apiEndpoint?: string | null
  isDisable: boolean
  isOnline: boolean
  /** 绑定渠道类型（当前仅 app），未绑定为 null */
  bindChannelType?: string | null
  /** 绑定渠道记录 id（应用为 app.id），未绑定为 null */
  bindChannelId?: string | null
  bindTime?: string | null
  createUserName?: string | null
  createTime?: string | null
  updateTime?: string | null
  updateUserName?: string | null
}

export interface WeixinWorkAppsResult {
  teamId: number
  /** 0=Member 1=Admin 2=Owner */
  myRole: number
  items: WeixinWorkAppItemView[]
}

export interface CreateWeixinWorkAppPayload {
  teamId: number
  name: string
  description?: string
  corpId: string
  corpSecret: string
  agentId: number
  token: string
  encodingAesKey: string
  /** API 地址覆盖，留空使用官方地址 */
  apiEndpoint?: string
  /** 创建即绑定的渠道类型（当前仅 'app'），不传表示不绑定 */
  channelType?: 'app'
  /** 创建即绑定的渠道记录 id（应用渠道为应用 id），与 channelType 同时提供 */
  channelId?: string
}

export interface UpdateWeixinWorkAppPayload {
  name: string
  description?: string
  corpId: string
  agentId: number
  /** 以下留空表示保持不变 */
  corpSecret?: string
  token?: string
  encodingAesKey?: string
  /** 留空表示使用官方地址 */
  apiEndpoint?: string
  isDisable: boolean
}

function toGuid(value: string): Guid {
  return value as unknown as Guid
}

/** 查询团队下的企业微信应用连接列表（含绑定渠道与状态） */
export async function getWeixinWorkApps(teamId: number, keyword?: string): Promise<WeixinWorkAppsResult> {
  const client = getApiClient()
  const res = await client.api.weixinwork_app.list.get({
    queryParameters: { teamId: String(teamId), keyword: keyword || undefined },
  })
  return {
    teamId: Number(res?.teamId ?? teamId),
    myRole: res?.myRole ?? 0,
    items: (res?.items ?? []).map((x) => ({
      weixinWorkAppId: x?.weixinWorkAppId as unknown as string,
      teamId: Number(x?.teamId ?? 0),
      name: x?.name ?? '',
      description: x?.description ?? null,
      corpId: x?.corpId ?? '',
      agentId: x?.agentId ?? 0,
      apiEndpoint: x?.apiEndpoint ?? null,
      isDisable: x?.isDisable ?? false,
      isOnline: x?.isOnline ?? false,
      bindChannelType: (x?.bindChannelType as unknown as string | null) ?? null,
      bindChannelId: x?.bindChannelId ?? null,
      bindTime: x?.bindTime ?? null,
      createUserName: x?.createUserName ?? null,
      createTime: x?.createTime ?? null,
      updateTime: x?.updateTime ?? null,
      updateUserName: x?.updateUserName ?? null,
    })),
  }
}

/** 创建企业微信应用连接（Admin+），携带渠道信息时创建即绑定；返回连接 id */
export async function createWeixinWorkApp(payload: CreateWeixinWorkAppPayload): Promise<string> {
  const client = getApiClient()
  const res = await client.api.weixinwork_app.post({
    teamId: String(payload.teamId),
    name: payload.name,
    description: payload.description ?? null,
    corpId: payload.corpId,
    corpSecret: payload.corpSecret,
    agentId: payload.agentId,
    token: payload.token,
    encodingAesKey: payload.encodingAesKey,
    apiEndpoint: payload.apiEndpoint ?? null,
    channelType: payload.channelType,
    channelId: payload.channelId ?? null,
  })
  return res?.value as unknown as string
}

/** 更新企业微信应用连接（corpSecret/token/encodingAesKey 留空表示保持不变） */
export async function updateWeixinWorkApp(weixinWorkAppId: string, payload: UpdateWeixinWorkAppPayload): Promise<void> {
  const client = getApiClient()
  await client.api.weixinwork_app.byId(toGuid(weixinWorkAppId)).put({
    name: payload.name,
    description: payload.description ?? null,
    corpId: payload.corpId,
    agentId: payload.agentId,
    corpSecret: payload.corpSecret || null,
    token: payload.token || null,
    encodingAesKey: payload.encodingAesKey || null,
    apiEndpoint: payload.apiEndpoint || null,
    isDisable: payload.isDisable,
  })
}

/** 删除企业微信应用连接（创建即绑定体系下删除即彻底移除，级联解除绑定） */
export async function deleteWeixinWorkApp(weixinWorkAppId: string): Promise<void> {
  const client = getApiClient()
  await client.api.weixinwork_app.byId(toGuid(weixinWorkAppId)).delete()
}

import { type Guid } from '@microsoft/kiota-abstractions'
import { getApiClient } from '@/api/kiota'

/** 钉钉应用连接列表项（不回显 AppSecret） */
export interface DingTalkAppItemView {
  dingTalkAppId: string
  teamId: number
  name: string
  description?: string | null
  /** 钉钉应用 AppKey（新版开发者后台为 Client ID） */
  appKey: string
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

export interface DingTalkAppsResult {
  teamId: number
  /** 0=Member 1=Admin 2=Owner */
  myRole: number
  items: DingTalkAppItemView[]
}

export interface CreateDingTalkAppPayload {
  teamId: number
  name: string
  description?: string
  appKey: string
  appSecret: string
  /** 创建即绑定的渠道类型（当前仅 'app'），不传表示不绑定 */
  channelType?: 'app'
  /** 创建即绑定的渠道记录 id（应用渠道为应用 id），与 channelType 同时提供 */
  channelId?: string
}

export interface UpdateDingTalkAppPayload {
  name: string
  description?: string
  /** 为空表示保持不变；修改后自动重连 */
  appKey?: string
  /** 为空表示保持不变 */
  appSecret?: string
  isDisable: boolean
}

function toGuid(value: string): Guid {
  return value as unknown as Guid
}

/** 查询团队下的钉钉应用连接列表（含绑定渠道与在线状态） */
export async function getDingTalkApps(teamId: number, keyword?: string): Promise<DingTalkAppsResult> {
  const client = getApiClient()
  const res = await client.api.dingtalk_app.list.get({
    queryParameters: { teamId: String(teamId), keyword: keyword || undefined },
  })
  return {
    teamId: Number(res?.teamId ?? teamId),
    myRole: res?.myRole ?? 0,
    items: (res?.items ?? []).map((x) => ({
      dingTalkAppId: x?.dingTalkAppId as unknown as string,
      teamId: Number(x?.teamId ?? 0),
      name: x?.name ?? '',
      description: x?.description ?? null,
      appKey: x?.appKey ?? '',
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

/** 创建钉钉应用连接（Admin+），携带渠道信息时创建即绑定；返回连接 id */
export async function createDingTalkApp(payload: CreateDingTalkAppPayload): Promise<string> {
  const client = getApiClient()
  const res = await client.api.dingtalk_app.post({
    teamId: String(payload.teamId),
    name: payload.name,
    description: payload.description ?? null,
    appKey: payload.appKey,
    appSecret: payload.appSecret,
    channelType: payload.channelType,
    channelId: payload.channelId ?? null,
  })
  return res?.value as unknown as string
}

/** 更新钉钉应用连接（名称/AppKey/密钥/启停；appKey、appSecret 留空表示保持不变） */
export async function updateDingTalkApp(dingTalkAppId: string, payload: UpdateDingTalkAppPayload): Promise<void> {
  const client = getApiClient()
  await client.api.dingtalk_app.byId(toGuid(dingTalkAppId)).put({
    name: payload.name,
    description: payload.description ?? null,
    appKey: payload.appKey || null,
    appSecret: payload.appSecret || null,
    isDisable: payload.isDisable,
  })
}

/** 删除钉钉应用连接（创建即绑定体系下删除即彻底移除，级联解除绑定） */
export async function deleteDingTalkApp(dingTalkAppId: string): Promise<void> {
  const client = getApiClient()
  await client.api.dingtalk_app.byId(toGuid(dingTalkAppId)).delete()
}

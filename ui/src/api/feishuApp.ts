import { type Guid } from '@microsoft/kiota-abstractions'
import { getApiClient } from '@/api/kiota'

/** 飞书应用连接列表项（不回显 AppSecret） */
export interface FeishuAppItemView {
  feishuAppId: string
  teamId: number
  name: string
  description?: string | null
  appId: string
  domain?: string | null
  isDisable: boolean
  isOnline: boolean
  /** 绑定渠道类型（当前仅 app），未绑定为 null */
  bindChannelType?: string | null
  /** 绑定渠道记录 id（应用为 app.id），未绑定为 null */
  bindChannelId?: string | null
  bindTime?: string | null
  createUserName?: string | null
  createTime?: string | null
}

export interface FeishuAppsResult {
  teamId: number
  /** 0=Member 1=Admin 2=Owner */
  myRole: number
  items: FeishuAppItemView[]
}

export interface CreateFeishuAppPayload {
  teamId: number
  name: string
  description?: string
  appId: string
  appSecret: string
  /** 为空表示飞书默认 https://open.feishu.cn */
  domain?: string
}

export interface UpdateFeishuAppPayload {
  name: string
  description?: string
  /** 为空表示保持不变 */
  appSecret?: string
  /** 为空表示保持不变 */
  domain?: string
  isDisable: boolean
}

function toGuid(value: string): Guid {
  return value as unknown as Guid
}

/** 查询团队下的飞书应用连接列表（含绑定渠道与在线状态） */
export async function getFeishuApps(teamId: number, keyword?: string): Promise<FeishuAppsResult> {
  const client = getApiClient()
  const res = await client.api.feishu_app.list.get({
    queryParameters: { teamId: String(teamId), keyword: keyword || undefined },
  })
  return {
    teamId: Number(res?.teamId ?? teamId),
    myRole: res?.myRole ?? 0,
    items: (res?.items ?? []).map((x) => ({
      feishuAppId: x?.feishuAppId as unknown as string,
      teamId: Number(x?.teamId ?? 0),
      name: x?.name ?? '',
      description: x?.description ?? null,
      appId: x?.appId ?? '',
      domain: x?.domain ?? null,
      isDisable: x?.isDisable ?? false,
      isOnline: x?.isOnline ?? false,
      bindChannelType: (x?.bindChannelType as unknown as string | null) ?? null,
      bindChannelId: x?.bindChannelId ?? null,
      bindTime: x?.bindTime ?? null,
      createUserName: x?.createUserName ?? null,
      createTime: x?.createTime ?? null,
    })),
  }
}

/** 创建飞书应用连接（Admin+），返回连接 id */
export async function createFeishuApp(payload: CreateFeishuAppPayload): Promise<string> {
  const client = getApiClient()
  const res = await client.api.feishu_app.post({
    teamId: String(payload.teamId),
    name: payload.name,
    description: payload.description ?? null,
    appId: payload.appId,
    appSecret: payload.appSecret,
    domain: payload.domain ?? null,
  })
  return res?.value as unknown as string
}

/** 更新飞书应用连接（名称/描述/密钥/域名/启停） */
export async function updateFeishuApp(feishuAppId: string, payload: UpdateFeishuAppPayload): Promise<void> {
  const client = getApiClient()
  await client.api.feishu_app.byId(toGuid(feishuAppId)).put({
    name: payload.name,
    description: payload.description ?? null,
    appSecret: payload.appSecret || null,
    domain: payload.domain ?? null,
    isDisable: payload.isDisable,
  })
}

/** 删除飞书应用连接（级联解除绑定） */
export async function deleteFeishuApp(feishuAppId: string): Promise<void> {
  const client = getApiClient()
  await client.api.feishu_app.byId(toGuid(feishuAppId)).delete()
}

/** 绑定飞书应用到渠道（同一飞书应用同时只能绑定一个渠道） */
export async function bindFeishuApp(feishuAppId: string, channelType: 'app', channelId: string): Promise<void> {
  const client = getApiClient()
  await client.api.feishu_app.byId(toGuid(feishuAppId)).bind.post({ channelType, channelId })
}

/** 解除飞书应用的渠道绑定 */
export async function unbindFeishuApp(feishuAppId: string): Promise<void> {
  const client = getApiClient()
  await client.api.feishu_app.byId(toGuid(feishuAppId)).unbind.post()
}

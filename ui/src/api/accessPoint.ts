import { type Guid } from '@microsoft/kiota-abstractions'
import { Env } from '@/config/env'
import { getApiClient } from '@/api/kiota'

/** 访问点公开配置（外部匿名端点返回结构，/openapi/external.json） */
export interface ExternalAccessPointResponse {
  appId?: string | null
  title?: string | null
  subtitle?: string | null
  placeholder?: string | null
  primaryColor?: string | null
  position?: string | null
  launcherText?: string | null
  avatar?: string | null
  panelWidth?: number | null
  panelHeight?: number | null
  defaultOpen?: boolean | null
  enabled?: boolean | null
}

export interface AppAccessPointConfig {
  appId?: string | null
  /** 面板标题，空则用应用名 */
  title?: string | null
  subtitle?: string | null
  placeholder?: string | null
  /** 主题色 #RRGGBB */
  primaryColor?: string | null
  /** bottomRight / bottomLeft */
  position?: string | null
  /** 悬浮按钮文案，空则用图标 */
  launcherText?: string | null
  /** 头像 objectKey */
  avatar?: string | null
  panelWidth?: number | null
  panelHeight?: number | null
  defaultOpen?: boolean | null
  enabled?: boolean | null
}

/** 保存访问点配置的请求体 */
export interface SaveAccessPointPayload {
  title?: string | null
  subtitle?: string | null
  placeholder?: string | null
  primaryColor?: string | null
  position?: string
  launcherText?: string | null
  avatar?: string | null
  panelWidth: number
  panelHeight: number
  defaultOpen: boolean
  enabled: boolean
}

function normalizeGuid(appId: string): Guid {
  return appId as unknown as Guid
}

/** 查询外部应用访问点配置（内部管理视图，未保存过返回默认值） */
export async function getAppAccessPoint(appId: string): Promise<AppAccessPointConfig> {
  const client = getApiClient()
  const res = await client.api.app.byId(normalizeGuid(appId)).accessPoint.get()
  return {
    appId: res?.appId as unknown as string | null,
    title: res?.title ?? null,
    subtitle: res?.subtitle ?? null,
    placeholder: res?.placeholder ?? null,
    primaryColor: res?.primaryColor ?? null,
    position: res?.position ?? 'bottomRight',
    launcherText: res?.launcherText ?? null,
    avatar: res?.avatar ?? null,
    panelWidth: res?.panelWidth ?? 380,
    panelHeight: res?.panelHeight ?? 560,
    defaultOpen: res?.defaultOpen ?? false,
    enabled: res?.enabled ?? true,
  }
}

/** 保存外部应用访问点配置（整体替换） */
export async function saveAppAccessPoint(appId: string, payload: SaveAccessPointPayload): Promise<void> {
  const client = getApiClient()
  await client.api.app.byId(normalizeGuid(appId)).accessPoint.put({
    title: payload.title ?? null,
    subtitle: payload.subtitle ?? null,
    placeholder: payload.placeholder ?? null,
    primaryColor: payload.primaryColor ?? null,
    position: payload.position,
    launcherText: payload.launcherText ?? null,
    avatar: payload.avatar ?? null,
    panelWidth: payload.panelWidth,
    panelHeight: payload.panelHeight,
    defaultOpen: payload.defaultOpen,
    enabled: payload.enabled,
  } as never)
}

/** 查询访问点公开配置（匿名，悬浮组件用）。
 * 外部接口（/api/external）已从内部 Kiota 客户端文档拆分（/openapi/external.json），此处直接 fetch。 */
export async function getExternalAccessPoint(appId: string): Promise<ExternalAccessPointResponse | null> {
  const res = await fetch(`${Env.serverUrl}/api/external/app/${appId}/access-point`)
  if (!res.ok) {
    return null
  }
  return (await res.json()) as ExternalAccessPointResponse
}

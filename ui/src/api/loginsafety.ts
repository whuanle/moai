import { getApiClient } from '@/api/kiota'

/** 登录日志记录 */
export interface LoginLogItem {
  id?: string
  userId?: number
  userName?: string
  /** 登录方式：0=账号密码，1=OAuth */
  loginType?: number
  isSuccess?: boolean
  failReason?: string | null
  ip?: string | null
  country?: string | null
  province?: string | null
  city?: string | null
  isp?: string | null
  userAgent?: string | null
  /** 风险等级：0=无 1=低 2=中 3=高 */
  riskLevel?: number
  riskReasons?: string | null
  createTime?: string | null
}

/** 登录风险事件记录 */
export interface LoginRiskEventItem {
  id?: string
  userId?: number
  userName?: string
  /** 规则编码 R1~R6 */
  ruleCode?: string | null
  /** 风险等级：2=中 3=高 */
  riskLevel?: number
  summary?: string | null
  detail?: string | null
  isRead?: boolean
  ip?: string | null
  province?: string | null
  city?: string | null
  createTime?: string | null
}

/** IP 封禁记录 */
export interface LoginIpBanItem {
  id?: string
  ip?: string | null
  reason?: string | null
  /** 来源：auto:R1 / auto:R2 / manual */
  sourceRule?: string | null
  expireTime?: string | null
  isLifted?: boolean
  createTime?: string | null
}

interface PagedResponse<T> {
  items?: T[] | null
  total?: number
  pageNo?: number
  pageSize?: number
}

export interface LoginLogQuery {
  pageNo?: number
  pageSize?: number
  userName?: string
  ip?: string
  isSuccess?: boolean
  riskLevel?: number
}

export interface RiskEventQuery {
  pageNo?: number
  pageSize?: number
  userName?: string
  isRead?: boolean
  riskLevel?: number
}

export interface IpBanQuery {
  pageNo?: number
  pageSize?: number
  ip?: string
}

/** 分页查询登录日志（管理员） */
export async function getLoginLogs(query: LoginLogQuery): Promise<PagedResponse<LoginLogItem>> {
  const client = getApiClient()
  return client.api.loginsafety.loginlog.get({ queryParameters: { ...query } }) as Promise<PagedResponse<LoginLogItem>>
}

/** 分页查询登录风险事件（管理员） */
export async function getRiskEvents(query: RiskEventQuery): Promise<PagedResponse<LoginRiskEventItem>> {
  const client = getApiClient()
  return client.api.loginsafety.riskevent.get({ queryParameters: { ...query } }) as Promise<PagedResponse<LoginRiskEventItem>>
}

/** 批量标记风险事件为已读（管理员） */
export async function markRiskEventsRead(eventIds: string[]): Promise<void> {
  const client = getApiClient()
  await client.api.loginsafety.riskevent.read.post({ eventIds })
}

/** 查询未读风险事件数量（菜单角标数据源，管理员） */
export async function getRiskEventUnreadCount(): Promise<number> {
  const client = getApiClient()
  const res = await client.api.loginsafety.riskevent.unreadCount.get()
  return res?.count ?? 0
}

/** 分页查询 IP 封禁记录（管理员） */
export async function getIpBans(query: IpBanQuery): Promise<PagedResponse<LoginIpBanItem>> {
  const client = getApiClient()
  return client.api.loginsafety.ipban.get({ queryParameters: { ...query } }) as Promise<PagedResponse<LoginIpBanItem>>
}

/** 解封 IP（管理员） */
export async function liftIpBan(id: string): Promise<void> {
  const client = getApiClient()
  await client.api.loginsafety.ipban.lift.post({ id })
}

/** 手动封禁 IP（管理员） */
export async function addIpBan(ip: string, minutes: number, reason: string): Promise<void> {
  const client = getApiClient()
  await client.api.loginsafety.ipban.add.post({ ip, minutes, reason })
}

/** 查询当前用户本人的登录记录（仅成功登录） */
export async function getMyLoginLogs(pageNo = 1, pageSize = 20): Promise<PagedResponse<LoginLogItem>> {
  const client = getApiClient()
  const res = await client.api.loginsafety.mylog.get({ queryParameters: { pageNo, pageSize } })
  return res as unknown as PagedResponse<LoginLogItem>
}

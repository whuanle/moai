import { getApiClient } from '@/api/kiota'
import type { GrantScopeDirection } from '@/utils/orgScopeTree'

/** 上架资源类型：app=应用，prompt=提示词，skill=技能（对齐后端 PublicationResourceType 枚举） */
export type PublicationResourceType = 'app' | 'prompt' | 'skill'

/** 上架审核状态：pending=待审核，approved=已通过，rejected=已驳回（对齐后端 PublicationState 枚举） */
export type PublicationState = 'pending' | 'approved' | 'rejected'

/** 应用上架可视范围：all=全员可见，organization=按组织限定（对齐后端 AppVisibleScope 枚举） */
export type AppVisibleScope = 'all' | 'organization'

/** 组织授权方向：self=仅该组织 up=该组织及以上 down=该组织及以下（对齐后端 OrgGrantScope 枚举，规范类型收敛在 utils/orgScopeTree） */
export type VisibleGrantDirection = GrantScopeDirection

/** 可视范围组织授权展示项（含组织名称） */
export interface VisibleOrgGrantInfo {
  orgRoleId?: number
  /** 组织名称（组织已删除时为空串） */
  roleName?: string | null
  scope?: VisibleGrantDirection
}

/** 上架审核记录：资源公开（is_public=true）前需系统管理员审批 */
export interface PublicationReviewItem {
  /** 后端 long 序列化为字符串 */
  publicationId?: string | null
  resourceType?: PublicationResourceType | null
  /** 应用为 app.id（uuid），提示词为 prompt.id（数字字符串），技能为 skill.id（uuid） */
  resourceId?: string | null
  /** 资源名称快照，申请时的名称 */
  resourceName?: string | null
  /** 资源头像 objectKey（取资源当前头像），未设置为空串 */
  resourceAvatar?: string | null
  /** 申请上架的版本号（int 编码 aaa*10000+bb*100+cc），仅应用类型有值 */
  publishVersion?: number | null
  /** 可视范围（仅应用类型使用）：all=全员可见，organization=按组织限定 */
  visibleScope?: AppVisibleScope | null
  /** 可视范围组织授权展示项（含组织名称），仅按组织限定时有值 */
  visibleOrgGrants?: VisibleOrgGrantInfo[] | null
  teamId?: string | null
  teamName?: string | null
  applyReason?: string | null
  state?: PublicationState | null
  reviewComment?: string | null
  reviewTime?: string | null
  createTime?: string | null
  /** 申请人 */
  createUserName?: string | null
  updateTime?: string | null
  /** 审批人（已审批时） */
  updateUserName?: string | null
}

/** 查询全平台的上架审核列表（仅系统管理员） */
export async function getPublicationList(
  params?: { state?: PublicationState; resourceType?: PublicationResourceType },
): Promise<PublicationReviewItem[]> {
  const client = getApiClient()
  const res = await client.api.publication.list.get({
    queryParameters: {
      state: params?.state,
      resourceType: params?.resourceType,
    },
  })
  return (res?.items ?? []) as PublicationReviewItem[]
}

/** 查询待审核上架申请数量（仅系统管理员，菜单「审批上架」角标数据源） */
export async function getPublicationPendingCount(): Promise<number> {
  const client = getApiClient()
  const res = await client.api.publication.pendingCount.get()
  return res?.count ?? 0
}

/** 查询团队的上架审核列表（团队成员可访问，用于查看申请与审批状态） */
export async function getTeamPublicationList(
  teamId: number,
  params?: { state?: PublicationState; resourceType?: PublicationResourceType },
): Promise<PublicationReviewItem[]> {
  const client = getApiClient()
  const res = await client.api.publication.team_list.get({
    queryParameters: {
      teamId: String(teamId),
      state: params?.state,
      resourceType: params?.resourceType,
    },
  })
  return (res?.items ?? []) as PublicationReviewItem[]
}

/** 申请上架，返回上架审核记录 id；需要资源所属团队 Admin+；应用类型必须已发布，携带可视范围（全员/按组织） */
export async function applyPublication(payload: {
  resourceType: PublicationResourceType
  resourceId: string
  applyReason?: string
  /** 应用版本号 aaa.bb.cc，仅 resourceType=app 必填 */
  version?: string
  /** 可视范围（仅 resourceType=app 支持）：all=全员可见（默认），organization=按组织限定 */
  visibleScope?: AppVisibleScope
  /** 可视范围组织授权清单（仅 visibleScope=organization 时携带），同一组织不可重复 */
  visibleOrgGrants?: { orgRoleId: number; scope: VisibleGrantDirection }[]
}): Promise<string> {
  const client = getApiClient()
  const res = await client.api.publication.apply.post({
    resourceType: payload.resourceType,
    resourceId: payload.resourceId,
    applyReason: payload.applyReason,
    version: payload.version,
    visibleScope: payload.visibleScope,
    visibleOrgGrants: payload.visibleOrgGrants,
  })
  return String(res?.value ?? '')
}

/** 撤回上架申请（仅待审核状态可撤回），需要资源所属团队 Admin+ */
export async function withdrawPublication(publicationId: string): Promise<void> {
  const client = getApiClient()
  await client.api.publication.withdraw.post({ publicationId })
}

/** 下架（取消公开）：将已公开资源从平台下架，不改变团队内发布状态；需要资源所属团队 Admin+ */
export async function takedownPublication(payload: {
  resourceType: PublicationResourceType
  resourceId: string
}): Promise<void> {
  const client = getApiClient()
  await client.api.publication.takedown.post({
    resourceType: payload.resourceType,
    resourceId: payload.resourceId,
  })
}

/** 审批上架申请（仅系统管理员）：isApprove=true 通过并公开资源，false 驳回 */
export async function reviewPublication(
  publicationId: string,
  isApprove: boolean,
  reviewComment?: string,
): Promise<void> {
  const client = getApiClient()
  await client.api.publication.review.post({ publicationId, isApprove, reviewComment })
}

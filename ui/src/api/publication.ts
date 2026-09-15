import { getApiClient } from '@/api/kiota'

/** 上架资源类型：app=应用，prompt=提示词（对齐后端 PublicationResourceType 枚举） */
export type PublicationResourceType = 'app' | 'prompt'

/** 上架审核状态：pending=待审核，approved=已通过，rejected=已驳回（对齐后端 PublicationState 枚举） */
export type PublicationState = 'pending' | 'approved' | 'rejected'

/** 上架审核记录：资源公开（is_public=true）前需系统管理员审批 */
export interface PublicationReviewItem {
  /** 后端 long 序列化为字符串 */
  publicationId?: string | null
  resourceType?: PublicationResourceType | null
  /** 应用为 app.id（uuid），提示词为 prompt.id（数字字符串） */
  resourceId?: string | null
  /** 资源名称快照，申请时的名称 */
  resourceName?: string | null
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

/** 申请上架，返回上架审核记录 id；需要资源所属团队 Admin+ */
export async function applyPublication(payload: {
  resourceType: PublicationResourceType
  resourceId: string
  applyReason?: string
}): Promise<string> {
  const client = getApiClient()
  const res = await client.api.publication.apply.post({
    resourceType: payload.resourceType,
    resourceId: payload.resourceId,
    applyReason: payload.applyReason,
  })
  return String(res?.value ?? '')
}

/** 撤回上架申请（仅待审核状态可撤回），需要资源所属团队 Admin+ */
export async function withdrawPublication(publicationId: string): Promise<void> {
  const client = getApiClient()
  await client.api.publication.withdraw.post({ publicationId })
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

import { getApiClient } from '@/api/kiota'

export interface SkillListItem {
  id?: string | null
  key?: string | null
  name?: string | null
  description?: string | null
  isSystem?: boolean | null
  isDisable?: boolean | null
  /** 所属团队 id，0=系统内置或个人技能 */
  teamId?: number | null
  /** 分类 id，0=未分类 */
  classifyId?: number | null
  /** 是否已上架市场公开 */
  isPublic?: boolean | null
  /** 待审核的上架申请 id（后端 long 序列化为字符串），无待审核申请时为 null */
  pendingPublicationId?: string | number | null
  fileCount?: number | null
  createUserId?: number | null
  createUserName?: string | null
  createTime?: string | null
  updateTime?: string | null
  updateUserName?: string | null
}

export interface SkillFileItem {
  path?: string | null
  fileId?: string | number | null
  fileName?: string | null
}

export interface SkillDetail {
  id?: string | null
  key?: string | null
  name?: string | null
  description?: string | null
  instructions?: string | null
  files?: SkillFileItem[] | null
  isSystem?: boolean | null
  isDisable?: boolean | null
  teamId?: number | null
  /** 分类 id，0=未分类 */
  classifyId?: number | null
  isPublic?: boolean | null
  createTime?: string | null
  updateTime?: string | null
}

export interface SkillFileDownloadItem {
  path?: string | null
  fileName?: string | null
  downloadUrl?: string | null
}

export interface SkillOption {
  id?: string | null
  key?: string | null
  name?: string | null
  description?: string | null
  isSystem?: boolean | null
  /** 所属团队 id，0=系统内置或个人技能 */
  teamId?: number | null
}

export interface GetSkillsParams {
  pageNo?: number
  pageSize?: number
  searchText?: string
  classifyId?: number
}

export interface SkillListFilters {
  keywords?: string
  classifyId?: number
}

export async function getSkills(params: GetSkillsParams): Promise<{ totalCount: number; items: SkillListItem[] }> {
  const client = getApiClient()
  const res = await client.api.skill.list.get({
    queryParameters: {
      pageNo: params.pageNo,
      pageSize: params.pageSize,
      searchText: params.searchText,
      classifyId: params.classifyId,
    },
  })
  return { totalCount: res?.totalCount ?? 0, items: res?.items ?? [] }
}

export async function getMySkills(filters?: SkillListFilters): Promise<SkillListItem[]> {
  const client = getApiClient()
  const res = await client.api.skill.my_list.get({
    queryParameters: {
      keywords: filters?.keywords || undefined,
      classifyId: filters?.classifyId || undefined,
    },
  })
  return (res?.items ?? []) as SkillListItem[]
}

export async function getTeamSkills(teamId: number, filters?: SkillListFilters): Promise<SkillListItem[]> {
  const client = getApiClient()
  const res = await client.api.skill.team_list.get({
    queryParameters: {
      teamId,
      keywords: filters?.keywords || undefined,
      classifyId: filters?.classifyId || undefined,
    },
  })
  return (res?.items ?? []) as SkillListItem[]
}

export async function getSkillMarketList(filters?: SkillListFilters): Promise<SkillListItem[]> {
  const client = getApiClient()
  const res = await client.api.skill.market_list.get({
    queryParameters: {
      keywords: filters?.keywords || undefined,
      classifyId: filters?.classifyId || undefined,
    },
  })
  return (res?.items ?? []) as SkillListItem[]
}

export async function getSkill(id: string): Promise<SkillDetail> {
  const client = getApiClient()
  return (await client.api.skill.byId(id).get()) ?? {}
}

export async function getSkillFileDownloadUrls(id: string): Promise<SkillFileDownloadItem[]> {
  const client = getApiClient()
  const res = await client.api.skill.byId(id).download.get()
  return (res?.items ?? []) as SkillFileDownloadItem[]
}

export async function createSkill(payload: {
  teamId?: number
  key: string
  name: string
  description?: string
  instructions?: string
  files: SkillFileItem[]
  classifyId?: number
}): Promise<string | undefined> {
  const client = getApiClient()
  const res = await client.api.skill.post({
    teamId: payload.teamId ?? 0,
    key: payload.key,
    name: payload.name,
    description: payload.description ?? '',
    instructions: payload.instructions ?? '',
    classifyId: payload.classifyId ?? 0,
    // Kiota 将后端 long 生成为 string，统一收敛为字符串
    files: payload.files.map((f) => ({
      path: f.path ?? '',
      fileId: f.fileId != null ? String(f.fileId) : '0',
      fileName: f.fileName ?? '',
    })),
  })
  return res?.value ?? undefined
}

export async function updateSkill(
  id: string,
  payload: { name: string; description?: string; instructions?: string; files: SkillFileItem[]; classifyId?: number },
): Promise<void> {
  const client = getApiClient()
  await client.api.skill.byId(id).put({
    name: payload.name,
    description: payload.description ?? '',
    instructions: payload.instructions ?? '',
    classifyId: payload.classifyId ?? 0,
    files: payload.files.map((f) => ({
      path: f.path ?? '',
      fileId: f.fileId != null ? String(f.fileId) : '0',
      fileName: f.fileName ?? '',
    })),
  })
}

export async function deleteSkill(id: string): Promise<void> {
  const client = getApiClient()
  await client.api.skill.byId(id).delete()
}

export async function setSkillDisable(id: string, isDisable: boolean): Promise<void> {
  const client = getApiClient()
  await client.api.skill.byId(id).disable.put({ isDisable })
}

export async function getSkillOptions(params?: { teamId?: number; includePersonal?: boolean }): Promise<SkillOption[]> {
  const client = getApiClient()
  const res = await client.api.skill.optionsPath.get({
    queryParameters: {
      teamId: params?.teamId ?? 0,
      includePersonal: params?.includePersonal ?? false,
    },
  })
  return res?.items ?? []
}

async function sha256(buffer: ArrayBuffer): Promise<string> {
  const hash = await crypto.subtle.digest('SHA-256', buffer)
  return Array.from(new Uint8Array(hash))
    .map((b) => b.toString(16).padStart(2, '0'))
    .join('')
}

/** 上传技能包文件，返回文件 id；已存在时直接复用. */
export async function uploadSkillFile(file: File): Promise<number> {
  const buffer = await file.arrayBuffer()
  const hash = await sha256(buffer)
  const client = getApiClient()
  const pre = await client.api.skill.file.preupload.post({
    fileName: file.name,
    contentType: file.type || 'application/octet-stream',
    fileSize: file.size,
    shA256: hash,
  })

  const fileIdRaw = pre?.fileId
  if (fileIdRaw == null) {
    throw new Error('preUploadFileFailed')
  }

  if (!pre?.isExist && pre?.uploadUrl) {
    const res = await fetch(pre.uploadUrl, {
      method: 'PUT',
      headers: { 'Content-Type': file.type || 'application/octet-stream' },
      body: file,
    })
    if (!res.ok) {
      throw new Error('uploadFileFailed')
    }
  }

  const fileId = Number(fileIdRaw)
  await client.api.skill.file.complete.post({ fileId: String(fileId), isSuccess: true })
  return fileId
}

/** 下载技能包：逐文件触发浏览器下载（预签名地址 1 小时有效）. */
export async function downloadSkillFiles(id: string): Promise<number> {
  const items = await getSkillFileDownloadUrls(id)
  items.forEach((item, index) => {
    if (!item.downloadUrl) return
    setTimeout(() => {
      const anchor = document.createElement('a')
      anchor.href = item.downloadUrl as string
      anchor.download = item.fileName || item.path || 'file'
      document.body.appendChild(anchor)
      anchor.click()
      anchor.remove()
    }, index * 300)
  })
  return items.length
}

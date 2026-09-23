import { getApiClient } from '@/api/kiota'
import { uploadImageWithKey } from '@/utils/storage'

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
  /** 技能头像的 ObjectKey（空串=未设置），前端用 resolveStorageUrl 转可访问地址 */
  avatarPath?: string | null
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
  /** 技能头像的 ObjectKey（空串=未设置） */
  avatarPath?: string | null
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
  /** 头像 objectKey：先走存储直传管线拿 key，再随创建请求一起提交（技能此时还不存在，无法调头像接口） */
  avatar?: string
}): Promise<string | undefined> {
  const client = getApiClient()
  const res = await client.api.skill.post({
    teamId: payload.teamId ?? 0,
    key: payload.key,
    name: payload.name,
    description: payload.description ?? '',
    instructions: payload.instructions ?? '',
    classifyId: payload.classifyId ?? 0,
    avatar: payload.avatar,
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

/** 设置技能头像（objectKey 为直传完成并登记的图片 key） */
export async function setSkillAvatar(id: string, objectKey: string): Promise<void> {
  const client = getApiClient()
  await client.api.skill.byId(id).avatar.post({ objectKey })
}

/** 上传图片并设为技能头像（走存储直传管线），返回公开访问地址 */
export async function uploadSkillAvatar(id: string, file: File): Promise<string> {
  const { objectKey, url } = await uploadImageWithKey(file)
  await setSkillAvatar(id, objectKey)
  return url
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

/** 压缩包解压结果：技能信息（来自 SKILL.md）+ 展开登记后的文件清单 */
export interface ExtractSkillPackageResult {
  name?: string | null
  description?: string | null
  instructions?: string | null
  files?: SkillFileItem[] | null
}

/** 解压已上传的技能压缩包：服务端展开为逐文件资源并解析 SKILL.md 技能信息 */
export async function extractSkillPackage(fileId: number): Promise<ExtractSkillPackageResult> {
  const client = getApiClient()
  const res = await client.api.skill.file.extract.post({ fileId: String(fileId) })
  return {
    name: res?.name,
    description: res?.description,
    instructions: res?.instructions,
    files: (res?.files ?? []).map((f) => ({
      path: f.path ?? '',
      fileId: f.fileId != null ? Number(f.fileId) : 0,
      fileName: f.fileName ?? '',
    })),
  }
}

/** 上传 zip 技能包并解压：三段上传后触发服务端解压，返回技能信息与文件清单 */
export async function uploadAndExtractSkillPackage(file: File): Promise<ExtractSkillPackageResult> {
  const fileId = await uploadSkillFile(file)
  return extractSkillPackage(fileId)
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

import { getApiClient } from '@/api/kiota'

export interface SkillListItem {
  id?: string | null
  key?: string | null
  name?: string | null
  description?: string | null
  isSystem?: boolean | null
  isDisable?: boolean | null
  fileCount?: number | null
  createTime?: string | null
  updateTime?: string | null
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
  createTime?: string | null
  updateTime?: string | null
}

export interface SkillOption {
  id?: string | null
  key?: string | null
  name?: string | null
  description?: string | null
  isSystem?: boolean | null
}

export interface GetSkillsParams {
  pageNo?: number
  pageSize?: number
  searchText?: string
}

export async function getSkills(params: GetSkillsParams): Promise<{ totalCount: number; items: SkillListItem[] }> {
  const client = getApiClient()
  const res = await client.api.skill.list.get({
    queryParameters: {
      pageNo: params.pageNo,
      pageSize: params.pageSize,
      searchText: params.searchText,
    },
  })
  return { totalCount: res?.totalCount ?? 0, items: res?.items ?? [] }
}

export async function getSkill(id: string): Promise<SkillDetail> {
  const client = getApiClient()
  return (await client.api.skill.byId(id).get()) ?? {}
}

export async function createSkill(payload: {
  key: string
  name: string
  description?: string
  instructions?: string
  files: SkillFileItem[]
}): Promise<string | undefined> {
  const client = getApiClient()
  const res = await client.api.skill.post({
    key: payload.key,
    name: payload.name,
    description: payload.description ?? '',
    instructions: payload.instructions ?? '',
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
  payload: { name: string; description?: string; instructions?: string; files: SkillFileItem[] },
): Promise<void> {
  const client = getApiClient()
  await client.api.skill.byId(id).put({
    name: payload.name,
    description: payload.description ?? '',
    instructions: payload.instructions ?? '',
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

export async function getSkillOptions(): Promise<SkillOption[]> {
  const client = getApiClient()
  const res = await client.api.skill.optionsPath.get()
  return res?.items ?? []
}

interface PreUploadResult {
  fileId: number
  isExist: boolean
  uploadUrl?: string | null
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

  if (!pre?.fileId) {
    throw new Error('preUploadFileFailed')
  }

  if (!pre.isExist && pre.uploadUrl) {
    const res = await fetch(pre.uploadUrl, {
      method: 'PUT',
      headers: { 'Content-Type': file.type || 'application/octet-stream' },
      body: file,
    })
    if (!res.ok) {
      throw new Error('uploadFileFailed')
    }
  }

  const fileId = Number(pre.fileId)
  await client.api.skill.file.complete.post({ fileId: String(fileId), isSuccess: true })
  return fileId
}

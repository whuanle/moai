import { customPluginApi } from '@/api/plugin'

/**
 * 计算文件 SHA-256 哈希（小写十六进制）.
 */
async function sha256(value: ArrayBuffer): Promise<string> {
  const hash = await crypto.subtle.digest('SHA-256', value)
  return Array.from(new Uint8Array(hash))
    .map((b) => b.toString(16).padStart(2, '0'))
    .join('')
}

export interface UploadedOpenApiFile {
  /** 已登记的文件 ID（直接传给 ImportOpenApiPluginCommand/UpdateOpenApiPluginCommand）. */
  fileId: string
}

/** 预上传响应最小结构（兼容管理员/团队两套生成客户端）. */
export interface PreUploadOpenApiResult {
  fileId?: string | null
  isExist?: boolean | null
  uploadUrl?: string | null
}

/** 预上传签名提供者. */
export type PreUploadOpenApiFn = (payload: {
  pluginName: string
  fileName: string
  contentType: string
  fileSize: number
  shA256: string
}) => Promise<PreUploadOpenApiResult | null>

/**
 * 使用指定预上传函数上传 OpenAPI 文件（.json/.yaml/.yml），返回文件 ID.
 * <para>
 * 流程：计算 SHA-256 -> 预上传获取签名 URL -> 直传 OSS（PUT）；文件已存在时直接复用 fileId。
 * </para>
 */
export async function uploadOpenApiFileWith(
  file: File,
  pluginName: string,
  preUpload: PreUploadOpenApiFn,
): Promise<UploadedOpenApiFile> {
  const buffer = await file.arrayBuffer()
  const shA256 = await sha256(buffer)

  const pre = await preUpload({
    pluginName,
    fileName: file.name,
    contentType: file.type || 'application/octet-stream',
    fileSize: file.size,
    shA256,
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

  return { fileId: pre.fileId }
}

/**
 * 上传 OpenAPI 文件（.json/.yaml/.yml），返回文件 ID（管理员预上传接口）.
 */
export async function uploadOpenApiFile(file: File, pluginName: string): Promise<UploadedOpenApiFile> {
  return uploadOpenApiFileWith(file, pluginName, (payload) =>
    customPluginApi.preUploadOpenApiFile({
      pluginName: payload.pluginName,
      fileName: payload.fileName,
      contentType: payload.contentType,
      fileSize: payload.fileSize,
      shA256: payload.shA256,
    }),
  )
}

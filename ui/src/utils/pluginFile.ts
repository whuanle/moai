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

/**
 * 上传 OpenAPI 文件（.json/.yaml/.yml），返回文件 ID.
 * <para>
 * 流程：计算 SHA-256 -> 预上传获取签名 URL（复用 /api/ai/plugin/custom/pre_upload_openapi 避免同名）-> 直传
 * OSS（PUT）-> 触发上传完成接口。文件已存在时直接复用 fileId。
 * </para>
 */
export async function uploadOpenApiFile(file: File, pluginName: string): Promise<UploadedOpenApiFile> {
  const buffer = await file.arrayBuffer()
  const shA256 = await sha256(buffer)

  const pre = await customPluginApi.preUploadOpenApiFile({
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

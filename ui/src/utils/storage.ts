import { getApiClient } from '@/api/kiota'
import { Env } from '@/config/env'
import { useAppStore } from '@/store/app'

/**
 * 判断是否为绝对 URL（http/https）.
 */
export function isAbsoluteUrl(value: string | null | undefined): boolean {
  return /^https?:\/\//i.test(value ?? '')
}

/**
 * 获取后端服务地址，优先使用服务端返回的 serviceUrl（对应 SystemOptions.Server）.
 */
function getServiceUrl(): string {
  const serviceUrl = useAppStore.getState().serverInfo?.serviceUrl
  return serviceUrl?.trim() || Env.serverUrl
}

/**
 * 根据存储的原值解析出可访问的完整地址.
 * <para>
 * 若为绝对 URL（手动填写的图标地址），直接返回；
 * 若为 osskey（上传的图标，数据库只存 osskey），则拼装为 {服务端地址}/static/{osskey}.
 * </para>
 */
export function resolveStorageUrl(value: string | null | undefined): string {
  const raw = value?.trim()
  if (!raw) return ''
  if (isAbsoluteUrl(raw)) return raw
  const base = getServiceUrl().replace(/\/+$/, '')
  const key = raw.replace(/^\/+/, '')
  return `${base}/static/${key}`
}

/**
 * 计算文件 SHA-256 哈希（小写十六进制）.
 */
async function sha256(value: ArrayBuffer): Promise<string> {
  const hash = await crypto.subtle.digest('SHA-256', value)
  return Array.from(new Uint8Array(hash))
    .map((b) => b.toString(16).padStart(2, '0'))
    .join('')
}

export interface UploadedImage {
  /** 文件在存储中的 ObjectKey（用于后端登记/持久化）. */
  objectKey: string
  /** 可访问的完整地址（http://{服务器}/static/{objectKey}）. */
  url: string
}

/**
 * 上传图片，返回文件 ObjectKey 和可访问的完整地址.
 * <para>
 * 流程：计算 SHA-256 -> 预上传获取签名地址 -> 直传 OSS -> 触发上传完成接口（该接口返回公开访问地址）。
 * 若后端未返回访问地址，则回退为由 objectKey 拼接的 /static 地址。
 * </para>
 */
export async function uploadImageWithKey(file: File): Promise<UploadedImage> {
  const client = getApiClient()
  const buffer = await file.arrayBuffer()
  const shA256 = await sha256(buffer)

  const pre = await client.api.storage.public.pre_upload_image.post({
    fileName: file.name,
    contentType: file.type || 'image/png',
    fileSize: file.size,
    shA256,
  })

  if (!pre?.objectKey) {
    throw new Error('uploadImageFailed')
  }

  if (!pre.isExist) {
    if (!pre.uploadUrl) {
      throw new Error('uploadImageFailed')
    }

    const res = await fetch(pre.uploadUrl, {
      method: 'PUT',
      headers: { 'Content-Type': file.type || 'application/octet-stream' },
      body: file,
    })
    if (!res.ok) {
      throw new Error('uploadImageFailed')
    }

    const complete = await client.api.storage.complate_url.post({
      fileId: pre.fileId ?? '',
      isSuccess: true,
    })

    if (complete?.accessUrl) {
      return { objectKey: pre.objectKey, url: complete.accessUrl }
    }
  }

  return { objectKey: pre.objectKey, url: resolveStorageUrl(pre.objectKey) }
}

/**
 * 上传图片，返回可访问的完整地址（http://{服务器}/static/public/...）.
 * <para>
 * 流程：计算 SHA-256 -> 预上传获取签名地址 -> 直传 OSS -> 触发上传完成接口（该接口返回公开访问地址）。
 * 若后端未返回访问地址，则回退为由 objectKey 拼接的 /static 地址。可直接保存返回值用于展示。
 * </para>
 */
export async function uploadImage(file: File): Promise<string> {
  return (await uploadImageWithKey(file)).url
}

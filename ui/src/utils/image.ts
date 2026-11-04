/**
 * 浏览器 canvas 是否支持编码 WebP（Safari 部分版本只解码不编码，toBlob 会静默回退 PNG）.
 * 结果缓存，避免每次选图重复探测.
 */
let webpEncodeSupported: boolean | null = null

function supportsWebpEncode(): boolean {
  if (webpEncodeSupported === null) {
    const canvas = document.createElement('canvas')
    canvas.width = 1
    canvas.height = 1
    webpEncodeSupported = canvas.toDataURL('image/webp').startsWith('data:image/webp')
  }
  return webpEncodeSupported
}

export interface ImageEncodeType {
  /** MIME 类型（image/webp / image/png / image/jpeg）. */
  type: string
  /** 对应文件扩展名（含点）. */
  ext: string
}

/**
 * 挑选图片编码格式：优先 WebP（体积小且支持透明），浏览器不支持编码时——
 * 需要透明（hasAlpha）回退 PNG，否则回退 JPEG.
 */
export function pickImageEncodeType(hasAlpha: boolean): ImageEncodeType {
  if (supportsWebpEncode()) {
    return { type: 'image/webp', ext: '.webp' }
  }
  return hasAlpha
    ? { type: 'image/png', ext: '.png' }
    : { type: 'image/jpeg', ext: '.jpg' }
}

function canvasToBlob(canvas: HTMLCanvasElement, type: string, quality?: number): Promise<Blob | null> {
  return new Promise((resolve) => {
    canvas.toBlob(resolve, type, quality)
  })
}

function renameExtension(fileName: string, ext: string): string {
  const base = fileName.replace(/\.[^.]+$/, '')
  return `${base || 'image'}${ext}`
}

export interface CompressImageOptions {
  /** 压缩后最大边长（px），仅缩小不放大，默认 512（头像展示最大 128px，留足高清屏余量）. */
  maxSide?: number
  /** 有损编码质量，默认 0.9（头像尺寸下视觉无损）. */
  quality?: number
  /** 小于该字节数的文件直接跳过，默认 200KB. */
  skipSize?: number
}

/**
 * 前端压缩头像类图片：canvas 降采样到 maxSide 内并重编码（优先 WebP），
 * 上传体积从数 MB 降到几十 KB，展示尺寸下视觉无损。
 * <para>
 * 跳过：非图片、SVG（矢量源重编码无收益）、GIF（重编码丢失动画）、小于 skipSize 的文件；
 * 解码失败或压缩无体积收益时原样返回，调用方无需判空.
 * </para>
 */
export async function compressImage(file: File, options: CompressImageOptions = {}): Promise<File> {
  const { maxSide = 512, quality = 0.9, skipSize = 200 * 1024 } = options
  const type = file.type.toLowerCase()
  if (!type.startsWith('image/') || type === 'image/svg+xml' || type === 'image/gif' || file.size <= skipSize) {
    return file
  }
  try {
    const bitmap = await createImageBitmap(file, { imageOrientation: 'from-image' })
    try {
      // 仅缩小不放大；非整数尺寸按比例取整
      const scale = Math.min(1, maxSide / Math.max(bitmap.width, bitmap.height))
      const width = Math.max(1, Math.round(bitmap.width * scale))
      const height = Math.max(1, Math.round(bitmap.height * scale))
      const canvas = document.createElement('canvas')
      canvas.width = width
      canvas.height = height
      const ctx = canvas.getContext('2d')
      if (!ctx) return file
      ctx.drawImage(bitmap, 0, 0, width, height)
      // PNG 源可能带透明，需按 hasAlpha 挑选回退格式
      const encode = pickImageEncodeType(type === 'image/png')
      const blob = await canvasToBlob(canvas, encode.type, quality)
      if (!blob || blob.size >= file.size) return file
      return new File([blob], renameExtension(file.name, encode.ext), { type: encode.type })
    } finally {
      bitmap.close()
    }
  } catch {
    // 解码失败（格式不受支持等）回退原图，由调用方原有校验与上传流程兜底
    return file
  }
}

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

// WebP 编码支持探测结果缓存在模块作用域，每个用例 resetModules 后动态加载以控制探测开关
let webpEncodeSupported = true
const drawImage = vi.fn()
const bitmapClose = vi.fn()

/** 动态加载被测模块（重置模块缓存使 supportsWebpEncode 重新探测）. */
async function loadImageUtils() {
  vi.resetModules()
  return import('../image')
}

function makeFile(sizeBytes: number, name = 'photo.png', type = 'image/png'): File {
  return new File([new Uint8Array(sizeBytes)], name, { type })
}

function stubBitmap(width: number, height: number) {
  bitmapClose.mockReset()
  vi.stubGlobal('createImageBitmap', vi.fn(async () => ({ width, height, close: bitmapClose })))
}

beforeEach(() => {
  webpEncodeSupported = true
  drawImage.mockReset()

  vi.spyOn(HTMLCanvasElement.prototype, 'getContext').mockReturnValue({ drawImage } as unknown as CanvasRenderingContext2D)
  vi.spyOn(HTMLCanvasElement.prototype, 'toDataURL').mockImplementation((type?: string) =>
    type === 'image/webp' && webpEncodeSupported ? 'data:image/webp;base64,AA' : 'data:image/png;base64,AA')
  vi.spyOn(HTMLCanvasElement.prototype, 'toBlob').mockImplementation(((callback: BlobCallback) => {
    callback(new Blob([new Uint8Array(10)], { type: 'image/webp' }))
  }) as typeof HTMLCanvasElement.prototype.toBlob)
})

afterEach(() => {
  vi.unstubAllGlobals()
  vi.restoreAllMocks()
})

describe('pickImageEncodeType', () => {
  it('支持 WebP 编码时一律返回 WebP（无论是否需要透明）', async () => {
    const { pickImageEncodeType } = await loadImageUtils()
    expect(pickImageEncodeType(true)).toEqual({ type: 'image/webp', ext: '.webp' })
    expect(pickImageEncodeType(false)).toEqual({ type: 'image/webp', ext: '.webp' })
  })

  it('不支持 WebP 编码时需要透明回退 PNG、否则回退 JPEG', async () => {
    webpEncodeSupported = false
    const { pickImageEncodeType } = await loadImageUtils()
    expect(pickImageEncodeType(true)).toEqual({ type: 'image/png', ext: '.png' })
    expect(pickImageEncodeType(false)).toEqual({ type: 'image/jpeg', ext: '.jpg' })
  })
})

describe('compressImage', () => {
  it('SVG/GIF/小文件/非图片直接跳过，返回原文件', async () => {
    const { compressImage } = await loadImageUtils()
    const svg = makeFile(300 * 1024, 'a.svg', 'image/svg+xml')
    const gif = makeFile(300 * 1024, 'a.gif', 'image/gif')
    const small = makeFile(1024, 'a.png', 'image/png')
    const text = makeFile(300 * 1024, 'a.txt', 'text/plain')
    await expect(compressImage(svg)).resolves.toBe(svg)
    await expect(compressImage(gif)).resolves.toBe(gif)
    await expect(compressImage(small)).resolves.toBe(small)
    await expect(compressImage(text)).resolves.toBe(text)
  })

  it('大图降采样到最大边长并重编码 WebP，扩展名随格式切换', async () => {
    stubBitmap(2000, 1000)
    const { compressImage } = await loadImageUtils()
    const file = makeFile(300 * 1024, 'photo.png', 'image/png')
    const result = await compressImage(file)
    expect(result).not.toBe(file)
    expect(result.name).toBe('photo.webp')
    expect(result.type).toBe('image/webp')
    expect(drawImage).toHaveBeenCalledWith(expect.anything(), 0, 0, 512, 256)
    expect(bitmapClose).toHaveBeenCalled()
  })

  it('小于最大边长的图片不放大，仅重编码', async () => {
    stubBitmap(300, 200)
    const { compressImage } = await loadImageUtils()
    const result = await compressImage(makeFile(300 * 1024))
    expect(result.name).toBe('photo.webp')
    expect(drawImage).toHaveBeenCalledWith(expect.anything(), 0, 0, 300, 200)
  })

  it('浏览器不支持 WebP 编码时 PNG 源回退 PNG 输出', async () => {
    webpEncodeSupported = false
    stubBitmap(1000, 1000)
    const { compressImage } = await loadImageUtils()
    const result = await compressImage(makeFile(300 * 1024, 'a.png', 'image/png'))
    expect(result.name).toBe('a.png')
    expect(result.type).toBe('image/png')
  })

  it('浏览器不支持 WebP 编码时 JPEG 源回退 JPEG 输出', async () => {
    webpEncodeSupported = false
    stubBitmap(1000, 1000)
    const { compressImage } = await loadImageUtils()
    const result = await compressImage(makeFile(300 * 1024, 'b.jpg', 'image/jpeg'))
    expect(result.name).toBe('b.jpg')
    expect(result.type).toBe('image/jpeg')
  })

  it('压缩无体积收益时保留原文件', async () => {
    stubBitmap(1000, 1000)
    vi.spyOn(HTMLCanvasElement.prototype, 'toBlob').mockImplementation(((callback: BlobCallback) => {
      callback(new Blob([new Uint8Array(400 * 1024)], { type: 'image/webp' }))
    }) as typeof HTMLCanvasElement.prototype.toBlob)
    const { compressImage } = await loadImageUtils()
    const file = makeFile(300 * 1024)
    await expect(compressImage(file)).resolves.toBe(file)
  })

  it('画布编码返回空 Blob 时保留原文件', async () => {
    stubBitmap(1000, 1000)
    vi.spyOn(HTMLCanvasElement.prototype, 'toBlob').mockImplementation(((callback: BlobCallback) => {
      callback(null)
    }) as typeof HTMLCanvasElement.prototype.toBlob)
    const { compressImage } = await loadImageUtils()
    const file = makeFile(300 * 1024)
    await expect(compressImage(file)).resolves.toBe(file)
  })

  it('图片解码失败时回退原文件', async () => {
    vi.stubGlobal('createImageBitmap', vi.fn(async () => {
      throw new Error('decode failed')
    }))
    const { compressImage } = await loadImageUtils()
    const file = makeFile(300 * 1024)
    await expect(compressImage(file)).resolves.toBe(file)
  })
})

/** 图表导出：把侧边栏当前渲染的 GPT-Vis 画布（canvas）导出为 PNG 下载. */

/**
 * 从容器内取图表画布并导出 PNG；G2/G6 以 devicePixelRatio 渲染，
 * canvas 像素尺寸即高清导出尺寸，直接平铺到白底画布即可.
 * 找不到画布（如 table 走 DOM 渲染）时返回 false.
 */
export async function exportChartImage(root: ParentNode, fileName: string): Promise<boolean> {
  const canvases = Array.from(root.querySelectorAll<HTMLCanvasElement>('.moai-chat__panel-chart canvas'))
  if (canvases.length === 0) return false
  const source = canvases.reduce((a, b) => (a.width * a.height >= b.width * b.height ? a : b))
  const out = document.createElement('canvas')
  out.width = source.width
  out.height = source.height
  const ctx = out.getContext('2d')
  if (!ctx) return false
  ctx.fillStyle = '#ffffff'
  ctx.fillRect(0, 0, out.width, out.height)
  ctx.drawImage(source, 0, 0)
  const blob = await new Promise<Blob | null>((resolve) => out.toBlob(resolve, 'image/png'))
  if (!blob) return false
  const url = URL.createObjectURL(blob)
  try {
    const link = document.createElement('a')
    link.href = url
    link.download = fileName
    document.body.appendChild(link)
    link.click()
    link.remove()
  } finally {
    URL.revokeObjectURL(url)
  }
  return true
}

/** 下载文件名：标题去掉文件系统非法字符，空回退 chart */
export function chartFileName(title: string): string {
  const base = title.replace(/[\\/:*?"<>|]+/g, ' ').trim()
  return `${base || 'chart'}.png`
}

/**
 * 时间展示统一格式：YYYY-MM-DD HH:mm（单行）。
 *
 * 项目规范出处：docs/user-management/sdd.md 决策 6（2026-09-02 UI 优化）——
 * 列表页时间列统一该格式，避免各浏览器 toLocaleString 的 locale 差异。
 */
export function formatDateTime(value: string | null | undefined): string {
  if (!value) return '-'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return '-'
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())} ${pad(date.getHours())}:${pad(date.getMinutes())}`
}

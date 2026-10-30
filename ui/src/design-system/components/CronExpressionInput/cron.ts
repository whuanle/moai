/**
 * 5 段 cron（分 时 日 月 周）纯函数集：解析/校验、常用模式互转（含时区换算）、下次执行推算。
 * 表达式值始终是提交口径（UTC）；展示/选择时区通过 offsetMinutes 参数换算。
 */

export interface CronFields {
  minute: string
  hour: string
  day: string
  month: string
  weekday: string
}

export type CronFieldKey = keyof CronFields

/** 各字段取值范围；周字段 0 与 7 都表示周日 */
export const FIELD_RANGES: Record<CronFieldKey, [number, number]> = {
  minute: [0, 59],
  hour: [0, 23],
  day: [1, 31],
  month: [1, 12],
  weekday: [0, 7],
}

export const CRON_FIELD_KEYS: CronFieldKey[] = ['minute', 'hour', 'day', 'month', 'weekday']

/** 常用（preset）模式：时间为展示时区口径 */
export type CronPreset =
  | { type: 'everyMinute' }
  | { type: 'intervalMinutes'; step: number }
  | { type: 'intervalHours'; step: number; minute: number }
  | { type: 'hourly'; minute: number }
  | { type: 'daily'; hour: number; minute: number }
  | { type: 'weekly'; weekdays: number[]; hour: number; minute: number }
  | { type: 'monthly'; days: number[]; hour: number; minute: number }

export type CronPresetType = CronPreset['type']

export const DEFAULT_PRESET: CronPreset = { type: 'daily', hour: 9, minute: 0 }

/** 拆分表达式为 5 个字段；段数不符返回 null */
export function splitCron(expr: string | null | undefined): CronFields | null {
  const parts = (expr ?? '').trim().split(/\s+/)
  if (parts.length !== 5 || parts.some((p) => p === '')) return null
  return { minute: parts[0], hour: parts[1], day: parts[2], month: parts[3], weekday: parts[4] }
}

const DIGITS_RE = /^\d+$/

/** 单字段语法校验：通配、数字、区间 a-b、步进（a-b/n 或 通配/n）及逗号组合，附带取值范围检查 */
export function isValidField(key: CronFieldKey, value: string): boolean {
  const v = value.trim()
  if (!v) return false
  const range = FIELD_RANGES[key]
  const min = range[0]
  const max = range[1]
  for (const part of v.split(',')) {
    if (part === '') return false
    const slashAt = part.indexOf('/')
    const rangePart = slashAt < 0 ? part : part.slice(0, slashAt)
    const stepPart = slashAt < 0 ? '' : part.slice(slashAt + 1)
    if (stepPart !== '' && (!DIGITS_RE.test(stepPart) || Number(stepPart) < 1)) return false
    let lo: number
    let hi: number
    if (rangePart === '*') {
      lo = min
      hi = max
    } else if (DIGITS_RE.test(rangePart)) {
      lo = Number(rangePart)
      hi = lo
    } else {
      const dashAt = rangePart.indexOf('-')
      const loStr = dashAt < 0 ? '' : rangePart.slice(0, dashAt)
      const hiStr = dashAt < 0 ? '' : rangePart.slice(dashAt + 1)
      if (dashAt < 0 || !DIGITS_RE.test(loStr) || !DIGITS_RE.test(hiStr)) return false
      lo = Number(loStr)
      hi = Number(hiStr)
    }
    if (lo < min || hi > max || lo > hi) return false
  }
  return true
}

export function isValidCron(expr: string | null | undefined): boolean {
  const f = splitCron(expr)
  return !!f && CRON_FIELD_KEYS.every((k) => isValidField(k, f[k]))
}

export function buildCron(f: CronFields): string {
  return [f.minute.trim(), f.hour.trim(), f.day.trim(), f.month.trim(), f.weekday.trim()].join(' ')
}

/** 周字段里的 7 归一为 0（周日），便于比较与反解 */
export function normalizeWeekday(field: string): string {
  return field
    .split(',')
    .map((p) => (p === '7' ? '0' : p))
    .join(',')
}

/**
 * 时刻（展示时区）→ UTC 时刻，返回 UTC 时分与日期偏移（-1/0/+1）。
 * offsetMinutes 为展示时区相对 UTC 的分钟偏移（如北京时间 480）。
 */
function shiftToUtc(hour: number, minute: number, offsetMinutes: number): { hour: number; minute: number; dayShift: number } {
  const total = hour * 60 + minute - offsetMinutes
  const dayShift = Math.floor(total / 1440)
  const norm = total - dayShift * 1440
  return { hour: Math.floor(norm / 60), minute: norm % 60, dayShift }
}

/** UTC 时刻 → 展示时区时刻，返回展示时区时分与日期偏移 */
function shiftFromUtc(hour: number, minute: number, offsetMinutes: number): { hour: number; minute: number; dayShift: number } {
  const total = hour * 60 + minute + offsetMinutes
  const dayShift = Math.floor(total / 1440)
  const norm = total - dayShift * 1440
  return { hour: Math.floor(norm / 60), minute: norm % 60, dayShift }
}

export const pad2 = (n: number) => String(n).padStart(2, '0')

/** 展示时区的 (hour, minute) 格式化为 HH:mm */
export function formatHM(hour: number, minute: number): string {
  return pad2(hour) + ':' + pad2(minute)
}

function positiveStep(step: number): number {
  return Math.max(1, Math.floor(step))
}

/** 升序去重 */
function dedupeSorted(nums: number[]): number[] {
  return [...new Set(nums)].sort((a, b) => a - b)
}

/**
 * 常用模式（展示时区）→ UTC 表达式。
 * 每月 1 日叠加向西借位（如北京时间 0~7 点）跨月界无法用标准 cron 表达，返回 null。
 */
export function presetToCron(preset: CronPreset, offsetMinutes = 0): string | null {
  switch (preset.type) {
    case 'everyMinute':
      return '* * * * *'
    case 'intervalMinutes':
      return ['*/' + positiveStep(preset.step), '*', '*', '*', '*'].join(' ')
    case 'intervalHours': {
      // 分钟仅随偏移的分钟部分平移（整点偏移如 UTC+8 不变）；小时步进相位无法用 cron 按时区校正，按原文生成
      const shifted = shiftToUtc(0, preset.minute, offsetMinutes)
      return [String(shifted.minute), '*/' + positiveStep(preset.step), '*', '*', '*'].join(' ')
    }
    case 'hourly': {
      const shifted = shiftToUtc(0, preset.minute, offsetMinutes)
      return [String(shifted.minute), '*', '*', '*', '*'].join(' ')
    }
    case 'daily': {
      const s = shiftToUtc(preset.hour, preset.minute, offsetMinutes)
      return [String(s.minute), String(s.hour), '*', '*', '*'].join(' ')
    }
    case 'weekly': {
      const s = shiftToUtc(preset.hour, preset.minute, offsetMinutes)
      const weekdays = dedupeSorted(preset.weekdays.map((w) => (w + s.dayShift + 7) % 7))
      return [String(s.minute), String(s.hour), '*', '*', weekdays.join(',')].join(' ')
    }
    case 'monthly': {
      const s = shiftToUtc(preset.hour, preset.minute, offsetMinutes)
      const days = dedupeSorted(preset.days.map((d) => d + s.dayShift))
      if (days.some((d) => d < 1 || d > 31)) return null
      return [String(s.minute), String(s.hour), days.join(','), '*', '*'].join(' ')
    }
  }
}

/** 逗号组合的纯数字列表（"1,3"）；含区间/步进返回 null */
function plainList(field: string): number[] | null {
  const parts = normalizeWeekday(field).split(',')
  const nums: number[] = []
  for (const p of parts) {
    if (!DIGITS_RE.test(p)) return null
    nums.push(Number(p))
  }
  return nums.length > 0 ? dedupeSorted(nums) : null
}

/**
 * UTC 表达式 → 常用模式（展示时区）；不属于任何常用形态返回 null（调用方回退自定义）。
 * 注意：intervalHours 的步进相位随时区漂移，只有 offsetMinutes 为 60 的整数倍时可无损往返。
 */
export function cronToPreset(expr: string, offsetMinutes = 0): CronPreset | null {
  const f = splitCron(expr)
  if (!f || !isValidCron(expr)) return null
  const minute = f.minute
  const hour = f.hour
  const day = f.day
  const month = f.month
  const weekday = normalizeWeekday(f.weekday)
  const dayAndMonthAny = day === '*' && month === '*'

  if (minute === '*' && hour === '*' && dayAndMonthAny && weekday === '*') return { type: 'everyMinute' }

  if (minute.startsWith('*/') && hour === '*' && dayAndMonthAny && weekday === '*') {
    const step = Number(minute.slice(2))
    return step > 1 ? { type: 'intervalMinutes', step } : { type: 'everyMinute' }
  }

  if (hour.startsWith('*/') && DIGITS_RE.test(minute) && dayAndMonthAny && weekday === '*') {
    const step = Number(hour.slice(2))
    if (step > 1) {
      // 展示时区口径的分钟：仅偏移的分钟部分参与平移
      const shifted = shiftFromUtc(0, Number(minute), offsetMinutes)
      return { type: 'intervalHours', step, minute: shifted.minute }
    }
    return { type: 'hourly', minute: Number(minute) }
  }

  if (DIGITS_RE.test(minute) && hour === '*' && dayAndMonthAny && weekday === '*') {
    return { type: 'hourly', minute: Number(minute) }
  }

  if (DIGITS_RE.test(minute) && DIGITS_RE.test(hour) && dayAndMonthAny && weekday === '*') {
    const s = shiftFromUtc(Number(hour), Number(minute), offsetMinutes)
    return { type: 'daily', hour: s.hour, minute: s.minute }
  }

  if (DIGITS_RE.test(minute) && DIGITS_RE.test(hour) && month === '*') {
    // 每周：日为 *，周为纯数字列表
    if (day === '*') {
      const weekdays = plainList(weekday)
      if (weekdays) {
        const s = shiftFromUtc(Number(hour), Number(minute), offsetMinutes)
        return { type: 'weekly', weekdays: weekdays.map((w) => (w + s.dayShift + 7) % 7), hour: s.hour, minute: s.minute }
      }
    }
    // 每月：周为 *，日为纯数字列表
    if (weekday === '*') {
      const days = plainList(day)
      if (days) {
        const s = shiftFromUtc(Number(hour), Number(minute), offsetMinutes)
        const shifted = days.map((d) => d + s.dayShift)
        if (shifted.every((d) => d >= 1 && d <= 31)) {
          return { type: 'monthly', days: shifted, hour: s.hour, minute: s.minute }
        }
        return null
      }
    }
  }

  return null
}

/** 表达式是否恰好匹配某个常用模式（用于决定初始 tab） */
export function isPresetCron(expr: string, offsetMinutes = 0): boolean {
  return cronToPreset(expr, offsetMinutes) !== null
}

/** 单字段匹配：支持通配、数字、区间、步进与逗号组合 */
export function fieldMatches(field: string, value: number, key: CronFieldKey): boolean {
  const v = key === 'weekday' ? normalizeWeekday(field) : field
  const range = FIELD_RANGES[key]
  const min = range[0]
  const max = range[1]
  for (const part of v.split(',')) {
    const slashAt = part.indexOf('/')
    const rangePart = slashAt < 0 ? part : part.slice(0, slashAt)
    const step = slashAt < 0 ? 1 : Number(part.slice(slashAt + 1))
    let lo: number
    let hi: number
    if (rangePart === '*') {
      lo = min
      hi = max
    } else if (DIGITS_RE.test(rangePart)) {
      lo = Number(rangePart)
      hi = lo
    } else {
      const dashAt = rangePart.indexOf('-')
      const loStr = dashAt < 0 ? '' : rangePart.slice(0, dashAt)
      const hiStr = dashAt < 0 ? '' : rangePart.slice(dashAt + 1)
      if (dashAt < 0 || !DIGITS_RE.test(loStr) || !DIGITS_RE.test(hiStr)) continue
      lo = Number(loStr)
      hi = Number(hiStr)
    }
    if (value >= lo && value <= hi && (value - lo) % step === 0) return true
  }
  return false
}

/**
 * 推算表达式的下次执行时刻（UTC）。
 * 从基准时间的下一整分钟起扫描（小时/日不匹配时跳步），一年窗口内无匹配返回 null。
 * 日与周同时受限时按 POSIX 语义取并集（OR），与 Hangfire/Cronos 一致。
 */
export function nextCronRun(expr: string, from: Date = new Date()): Date | null {
  const f = splitCron(expr)
  if (!f || !isValidCron(expr)) return null

  const cursor = new Date(from)
  cursor.setUTCSeconds(0, 0)
  cursor.setUTCMinutes(cursor.getUTCMinutes() + 1)
  const limit = cursor.getTime() + 366 * 86400_000

  while (cursor.getTime() <= limit) {
    if (!fieldMatches(f.month, cursor.getUTCMonth() + 1, 'month')) {
      cursor.setUTCHours(0, 0, 0, 0)
      cursor.setUTCDate(1)
      cursor.setUTCMonth(cursor.getUTCMonth() + 1)
      continue
    }
    const domMatch = fieldMatches(f.day, cursor.getUTCDate(), 'day')
    const dowMatch = fieldMatches(f.weekday, cursor.getUTCDay(), 'weekday')
    const dayRestricted = f.day !== '*' && f.weekday !== '*'
    const dayOk = dayRestricted ? domMatch || dowMatch : domMatch && dowMatch
    if (!dayOk) {
      cursor.setUTCHours(0, 0, 0, 0)
      cursor.setUTCDate(cursor.getUTCDate() + 1)
      continue
    }
    if (!fieldMatches(f.hour, cursor.getUTCHours(), 'hour')) {
      cursor.setUTCMinutes(0)
      cursor.setUTCHours(cursor.getUTCHours() + 1)
      continue
    }
    if (fieldMatches(f.minute, cursor.getUTCMinutes(), 'minute')) {
      return new Date(cursor)
    }
    cursor.setUTCMinutes(cursor.getUTCMinutes() + 1)
  }
  return null
}

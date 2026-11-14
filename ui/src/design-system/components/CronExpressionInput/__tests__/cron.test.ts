import { describe, expect, it } from 'vitest'
import {
  cronToPreset,
  isValidCron,
  isValidField,
  nextCronRun,
  presetToCron,
} from '../cron'

describe('isValidField / isValidCron', () => {
  it('接受标准 5 段表达式与 , - / * 组合', () => {
    expect(isValidCron('0 1 * * *')).toBe(true)
    expect(isValidCron('* * * * *')).toBe(true)
    expect(isValidCron('*/15 * * * *')).toBe(true)
    expect(isValidCron('1-10/2 * * * *')).toBe(true)
    expect(isValidCron('0 0 1,15 * 1-5')).toBe(true)
    expect(isValidCron('0 1 * * 7')).toBe(true) // 7 也是周日
  })

  it('拒绝段数不符/越界/非法字符', () => {
    expect(isValidCron('0 1 * *')).toBe(false)
    expect(isValidCron('* * * * * *')).toBe(false)
    expect(isValidCron('')).toBe(false)
    expect(isValidCron('60 * * * *')).toBe(false)
    expect(isValidCron('* 25 * * *')).toBe(false)
    expect(isValidCron('0 0 0 * *')).toBe(false) // 日从 1 开始
    expect(isValidCron('0 0 32 * *')).toBe(false)
    expect(isValidCron('a * * * *')).toBe(false)
    expect(isValidCron('*/0 * * * *')).toBe(false)
    expect(isValidCron('1-5-9 * * * *')).toBe(false)
    expect(isValidField('minute', '5-3')).toBe(false) // lo > hi
  })
})

describe('presetToCron（UTC 生成）', () => {
  it('offset 0 时所见即所得', () => {
    expect(presetToCron({ type: 'daily', hour: 9, minute: 0 }, 0)).toBe('0 9 * * *')
    expect(presetToCron({ type: 'weekly', weekdays: [1, 3], hour: 9, minute: 30 }, 0)).toBe('30 9 * * 1,3')
    expect(presetToCron({ type: 'monthly', days: [1], hour: 9, minute: 0 }, 0)).toBe('0 9 1 * *')
    expect(presetToCron({ type: 'everyMinute' }, 0)).toBe('* * * * *')
    expect(presetToCron({ type: 'intervalMinutes', step: 5 }, 0)).toBe('*/5 * * * *')
    expect(presetToCron({ type: 'intervalHours', step: 3, minute: 30 }, 0)).toBe('30 */3 * * *')
    expect(presetToCron({ type: 'hourly', minute: 30 }, 0)).toBe('30 * * * *')
  })

  it('北京时间（+480）向西平移 8 小时，天字段随借位联动', () => {
    expect(presetToCron({ type: 'daily', hour: 9, minute: 0 }, 480)).toBe('0 1 * * *')
    // 3 点借位到前一天 19 点：每天语义不变（day=*），仅小时回退
    expect(presetToCron({ type: 'daily', hour: 3, minute: 0 }, 480)).toBe('0 19 * * *')
    // 周一 3 点（北京）= 周日 19 点 UTC
    expect(presetToCron({ type: 'weekly', weekdays: [1], hour: 3, minute: 0 }, 480)).toBe('0 19 * * 0')
    expect(presetToCron({ type: 'weekly', weekdays: [1], hour: 9, minute: 0 }, 480)).toBe('0 1 * * 1')
    // 2 日 3 点（北京）= 1 日 19 点 UTC
    expect(presetToCron({ type: 'monthly', days: [2], hour: 3, minute: 0 }, 480)).toBe('0 19 1 * *')
  })

  it('每月 1 日叠加借位跨月界无法表达，返回 null', () => {
    expect(presetToCron({ type: 'monthly', days: [1], hour: 3, minute: 0 }, 480)).toBeNull()
    expect(presetToCron({ type: 'monthly', days: [1], hour: 9, minute: 0 }, 480)).toBe('0 1 1 * *')
  })
})

describe('cronToPreset（表达式反解回显）', () => {
  it('常用形态可反解，时区逆向还原', () => {
    expect(cronToPreset('0 1 * * *', 480)).toEqual({ type: 'daily', hour: 9, minute: 0 })
    expect(cronToPreset('0 19 * * 0', 480)).toEqual({ type: 'weekly', weekdays: [1], hour: 3, minute: 0 })
    expect(cronToPreset('*/5 * * * *', 480)).toEqual({ type: 'intervalMinutes', step: 5 })
    expect(cronToPreset('30 */3 * * *', 480)).toEqual({ type: 'intervalHours', step: 3, minute: 30 })
    expect(cronToPreset('30 * * * *', 480)).toEqual({ type: 'hourly', minute: 30 })
    expect(cronToPreset('0 0 31 * *', 480)).toEqual({ type: 'monthly', days: [31], hour: 8, minute: 0 })
    expect(cronToPreset('15 8 * * 1,3', 0)).toEqual({ type: 'weekly', weekdays: [1, 3], hour: 8, minute: 15 })
    expect(cronToPreset('* * * * *', 0)).toEqual({ type: 'everyMinute' })
  })

  it('非常用形态返回 null（回退自定义）', () => {
    expect(cronToPreset('5-10 * * * *', 0)).toBeNull()
    expect(cronToPreset('0 12 * * 1-5', 0)).toBeNull() // 周字段区间
    expect(cronToPreset('0 16 31 * *', 480)).toBeNull() // 31 日 + 东向借位 = 北京下月 1 日，跨月界无法表达
    expect(cronToPreset('not a cron', 0)).toBeNull()
  })

  it('每月形态按展示时区逆向还原', () => {
    // 2 日 16:00 UTC = 北京 3 日 0:00
    expect(cronToPreset('0 16 2 * *', 480)).toEqual({ type: 'monthly', days: [3], hour: 0, minute: 0 })
  })

  it('preset ↔ cron 往返一致', () => {
    const roundtrip = (expr: string, offset: number) => presetToCron(cronToPreset(expr, offset)!, offset)
    expect(roundtrip('0 1 * * *', 480)).toBe('0 1 * * *')
    expect(roundtrip('30 9 * * 1,3', 0)).toBe('30 9 * * 1,3')
    expect(roundtrip('*/15 * * * *', 0)).toBe('*/15 * * * *')
    expect(roundtrip('0 19 * * 0', 480)).toBe('0 19 * * 0')
    expect(roundtrip('0 19 5 * *', 480)).toBe('0 19 5 * *')
  })
})

describe('nextCronRun（下次执行推算，UTC）', () => {
  it('按分钟对齐到下一匹配时刻', () => {
    expect(nextCronRun('0 1 * * *', new Date('2026-10-30T00:30:00Z'))?.toISOString()).toBe('2026-10-30T01:00:00.000Z')
    expect(nextCronRun('*/15 * * * *', new Date('2026-10-30T00:07:00Z'))?.toISOString()).toBe('2026-10-30T00:15:00.000Z')
    // 基准恰在触发点：取下一轮而非当轮
    expect(nextCronRun('0 1 * * *', new Date('2026-10-30T01:00:00Z'))?.toISOString()).toBe('2026-10-31T01:00:00.000Z')
  })

  it('周字段跨日与 7=周日 归一', () => {
    // 2026-10-30 是周五：下一个周一 09:00 UTC
    expect(nextCronRun('0 9 * * 1', new Date('2026-10-30T12:00:00Z'))?.toISOString()).toBe('2026-11-02T09:00:00.000Z')
    // 2026-11-01 是周日；7 与 0 等价
    expect(nextCronRun('0 0 * * 7', new Date('2026-10-31T12:00:00Z'))?.toISOString()).toBe('2026-11-01T00:00:00.000Z')
  })

  it('月字段跨月推算', () => {
    expect(nextCronRun('0 0 1 * *', new Date('2026-10-30T00:00:00Z'))?.toISOString()).toBe('2026-11-01T00:00:00.000Z')
  })

  it('非法表达式返回 null', () => {
    expect(nextCronRun('bad', new Date())).toBeNull()
    expect(nextCronRun('61 * * * *', new Date())).toBeNull()
  })
})

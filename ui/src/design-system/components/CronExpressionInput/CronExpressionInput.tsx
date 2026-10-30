/**
 * CronExpressionInput：5 段 cron（分 时 日 月 周）表达式选择器，design-system 全局复用组件。
 * - 「常用」tab：每分钟/按间隔/每小时/每天/每周/每月 六类模式，控件按展示时区选择，生成时换算为 UTC 提交口径；
 * - 「自定义」tab：按字段直接编辑，支持 , - / 与 * 组合，实时校验；
 * - 底部预览：表达式原文 + 人类可读描述 + 下次执行时间（按展示时区）。
 * 受控协议：value 为 5 段表达式（提交口径 UTC），非法/未完成选择时 onChange 发空串交由表单必填校验兜底。
 */

import { useMemo, useState } from 'react'
import { Input, InputNumber, Segmented, Select, Typography, theme } from 'antd'
import { useTranslation } from 'react-i18next'
import { spacing } from '@/design-system/theme'
import {
  CRON_FIELD_KEYS,
  DEFAULT_PRESET,
  buildCron,
  cronToPreset,
  formatHM,
  isValidCron,
  isValidField,
  nextCronRun,
  pad2,
  presetToCron,
  splitCron,
  type CronFields,
  type CronFieldKey,
  type CronPreset,
} from './cron'

const { Text } = Typography

export interface CronExpressionInputProps {
  /** 表达式值（5 段，提交口径 UTC）；空值时按默认常用模式展示 */
  value?: string
  onChange?: (value: string) => void
  disabled?: boolean
  /** 展示/选择时区相对 UTC 的分钟偏移（如北京时间 480）；表达式值始终按 UTC 生成，默认 0 所见即所得 */
  displayOffsetMinutes?: number
  /** 下次执行推算的基准时间（默认当前时间；测试可固定） */
  now?: Date
  style?: React.CSSProperties
}

/** 常用模式在 UI 层的频率类型（把 intervalMinutes/intervalHours 合并为 interval + 单位） */
type CronUIFreqType = 'everyMinute' | 'interval' | 'hourly' | 'daily' | 'weekly' | 'monthly'

/** 常用模式的编辑态（比 CronPreset 扁平，方便控件双向绑定） */
interface PresetState {
  type: CronUIFreqType
  step: number
  intervalUnit: 'minute' | 'hour'
  minute: number
  hour: number
  weekdays: number[]
  days: number[]
}

function toPreset(s: PresetState): CronPreset {
  switch (s.type) {
    case 'everyMinute':
      return { type: 'everyMinute' }
    case 'interval':
      return s.intervalUnit === 'minute'
        ? { type: 'intervalMinutes', step: s.step }
        : { type: 'intervalHours', step: s.step, minute: s.minute }
    case 'hourly':
      return { type: 'hourly', minute: s.minute }
    case 'daily':
      return { type: 'daily', hour: s.hour, minute: s.minute }
    case 'weekly':
      return { type: 'weekly', weekdays: s.weekdays, hour: s.hour, minute: s.minute }
    case 'monthly':
      return { type: 'monthly', days: s.days, hour: s.hour, minute: s.minute }
  }
}

function fromPreset(p: CronPreset): PresetState {
  const base: PresetState = { type: 'daily', step: 5, intervalUnit: 'minute', minute: 0, hour: 9, weekdays: [1], days: [1] }
  switch (p.type) {
    case 'everyMinute':
      return { ...base, type: 'everyMinute' }
    case 'intervalMinutes':
      return { ...base, type: 'interval', intervalUnit: 'minute', step: p.step }
    case 'intervalHours':
      return { ...base, type: 'interval', intervalUnit: 'hour', step: p.step, minute: p.minute }
    case 'hourly':
      return { ...base, type: 'hourly', minute: p.minute }
    case 'daily':
      return { ...base, type: 'daily', hour: p.hour, minute: p.minute }
    case 'weekly':
      return { ...base, type: 'weekly', weekdays: p.weekdays, hour: p.hour, minute: p.minute }
    case 'monthly':
      return { ...base, type: 'monthly', days: p.days, hour: p.hour, minute: p.minute }
  }
}

function tzLabel(offsetMinutes: number): string {
  if (offsetMinutes === 0) return 'UTC'
  const sign = offsetMinutes > 0 ? '+' : '-'
  const abs = Math.abs(offsetMinutes)
  const h = Math.floor(abs / 60)
  const m = abs % 60
  return 'UTC' + sign + h + (m ? ':' + pad2(m) : '')
}

/** UTC 时刻按展示时区格式化为 YYYY-MM-DD HH:mm */
function formatInOffset(d: Date, offsetMinutes: number): string {
  const shifted = new Date(d.getTime() + offsetMinutes * 60000)
  const date = [shifted.getUTCFullYear(), pad2(shifted.getUTCMonth() + 1), pad2(shifted.getUTCDate())].join('-')
  const time = [pad2(shifted.getUTCHours()), pad2(shifted.getUTCMinutes())].join(':')
  return date + ' ' + time
}

const FIELD_LABEL_KEY: Record<CronFieldKey, string> = {
  minute: 'ds.cron.fieldMinute',
  hour: 'ds.cron.fieldHour',
  day: 'ds.cron.fieldDay',
  month: 'ds.cron.fieldMonth',
  weekday: 'ds.cron.fieldWeek',
}

const ALL_WEEKDAYS = [1, 2, 3, 4, 5, 6, 0]
const ALL_DAYS = Array.from({ length: 31 }, (_, i) => i + 1)

export function CronExpressionInput({
  value,
  onChange,
  disabled = false,
  displayOffsetMinutes = 0,
  now,
  style,
}: CronExpressionInputProps) {
  const { t } = useTranslation()
  const { token } = theme.useToken()

  // 初值只在挂载时从 value 反解一次（弹窗 destroyOnHidden 场景每次打开重挂载）
  const initial = useMemo(() => {
    const preset = value ? cronToPreset(value, displayOffsetMinutes) : null
    return {
      tab: preset || !value ? ('preset' as const) : ('custom' as const),
      preset: fromPreset(preset ?? DEFAULT_PRESET),
      fields: splitCron(value ?? '') ?? { minute: '*', hour: '*', day: '*', month: '*', weekday: '*' },
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])
  const [tab, setTab] = useState<'preset' | 'custom'>(initial.tab)
  const [presetState, setPresetState] = useState<PresetState>(initial.preset)
  const [fields, setFields] = useState<CronFields>(initial.fields)

  const baseNow = useMemo(() => now ?? new Date(), [now])

  const emitPreset = (next: PresetState) => {
    setPresetState(next)
    onChange?.(presetToCron(toPreset(next), displayOffsetMinutes) ?? '')
  }

  const setField = (key: CronFieldKey, v: string) => {
    const next = { ...fields, [key]: v }
    setFields(next)
    // 全部字段合法才外发，否则发空串交给表单必填校验
    if (CRON_FIELD_KEYS.every((k) => isValidField(k, next[k]))) {
      onChange?.(buildCron(next))
    } else {
      onChange?.('')
    }
  }

  const freqOptions: { value: CronUIFreqType; label: string }[] = [
    { value: 'everyMinute', label: t('ds.cron.freqEveryMinute') },
    { value: 'interval', label: t('ds.cron.freqInterval') },
    { value: 'hourly', label: t('ds.cron.freqHourly') },
    { value: 'daily', label: t('ds.cron.freqDaily') },
    { value: 'weekly', label: t('ds.cron.freqWeekly') },
    { value: 'monthly', label: t('ds.cron.freqMonthly') },
  ]

  // 每月 1 日叠加向西借位（dayShift=-1）跨月界无法表达：按当前时分动态禁用 1 日
  const monthlyBorrow = useMemo(() => {
    if (presetState.type !== 'monthly') return false
    const cron = presetToCron(toPreset(presetState), displayOffsetMinutes)
    // 借位判定独立于日期：直接用时分算（与 cron.ts shiftToUtc 同式）
    const total = presetState.hour * 60 + presetState.minute - displayOffsetMinutes
    return Math.floor(total / 1440) < 0 && cron === null
  }, [presetState, displayOffsetMinutes])

  const nextRun = useMemo(() => {
    if (!value || !isValidCron(value)) return null
    return nextCronRun(value, baseNow)
  }, [value, baseNow])

  const descText = useMemo(() => {
    // 有值按值反解；空值在常用 tab 下按当前编辑态描述（尚未外发，但预览应与将生成的表达式一致）
    const p = value ? cronToPreset(value, displayOffsetMinutes) : tab === 'preset' ? toPreset(presetState) : null
    if (!p) return value ? t('ds.cron.descCustom') : ''
    const sep = t('ds.cron.listSeparator')
    switch (p.type) {
      case 'everyMinute':
        return t('ds.cron.descEveryMinute')
      case 'intervalMinutes':
        return t('ds.cron.descIntervalMinutes', { n: p.step })
      case 'intervalHours':
        return t('ds.cron.descIntervalHours', { n: p.step, minute: p.minute })
      case 'hourly':
        return t('ds.cron.descHourly', { minute: p.minute })
      case 'daily':
        return t('ds.cron.descDaily', { time: formatHM(p.hour, p.minute) })
      case 'weekly':
        return t('ds.cron.descWeekly', {
          weekdays: p.weekdays.map((w) => t('ds.cron.weekday' + w)).join(sep),
          time: formatHM(p.hour, p.minute),
        })
      case 'monthly':
        return t('ds.cron.descMonthly', { days: p.days.join(sep), time: formatHM(p.hour, p.minute) })
    }
  }, [value, displayOffsetMinutes, tab, presetState, t])

  const invalid = !!value && !isValidCron(value)
  // 自定义 tab 的提示跟字段编辑态走（外发空串时 value 为假，单看 value 会漏提示）
  const customInvalid = tab === 'custom' && !CRON_FIELD_KEYS.every((k) => isValidField(k, fields[k]))

  const renderTimePair = () => (
    <>
      <InputNumber
        min={0}
        max={23}
        precision={0}
        value={presetState.hour}
        disabled={disabled}
        onChange={(v) => emitPreset({ ...presetState, hour: Number(v ?? 0) })}
        style={{ width: 64 }}
      />
      <Text type="secondary">{t('ds.cron.timeSeparator')}</Text>
      <InputNumber
        min={0}
        max={59}
        precision={0}
        value={presetState.minute}
        disabled={disabled}
        onChange={(v) => emitPreset({ ...presetState, minute: Number(v ?? 0) })}
        style={{ width: 64 }}
      />
    </>
  )

  const renderPresetControls = () => {
    switch (presetState.type) {
      case 'everyMinute':
        return null
      case 'interval':
        return (
          <>
            <Text type="secondary">{t('ds.cron.everyPrefix')}</Text>
            <InputNumber
              min={1}
              max={presetState.intervalUnit === 'minute' ? 59 : 23}
              precision={0}
              value={presetState.step}
              disabled={disabled}
              onChange={(v) => emitPreset({ ...presetState, step: Math.max(1, Number(v ?? 1)) })}
              style={{ width: 72 }}
            />
            <Segmented
              size="small"
              value={presetState.intervalUnit}
              disabled={disabled}
              onChange={(v) => emitPreset({ ...presetState, intervalUnit: v as 'minute' | 'hour' })}
              options={[
                { value: 'minute', label: t('ds.cron.intervalUnitMinutes') },
                { value: 'hour', label: t('ds.cron.intervalUnitHours') },
              ]}
            />
            {presetState.intervalUnit === 'hour' && (
              <>
                <Text type="secondary">{t('ds.cron.atMinutePrefix')}</Text>
                <InputNumber
                  min={0}
                  max={59}
                  precision={0}
                  value={presetState.minute}
                  disabled={disabled}
                  onChange={(v) => emitPreset({ ...presetState, minute: Number(v ?? 0) })}
                  style={{ width: 64 }}
                />
                <Text type="secondary">{t('ds.cron.atMinuteSuffix')}</Text>
              </>
            )}
          </>
        )
      case 'hourly':
        return (
          <>
            <Text type="secondary">{t('ds.cron.atMinutePrefix')}</Text>
            <InputNumber
              min={0}
              max={59}
              precision={0}
              value={presetState.minute}
              disabled={disabled}
              onChange={(v) => emitPreset({ ...presetState, minute: Number(v ?? 0) })}
              style={{ width: 64 }}
            />
            <Text type="secondary">{t('ds.cron.atMinuteSuffix')}</Text>
          </>
        )
      case 'daily':
        return renderTimePair()
      case 'weekly':
        return (
          <>
            <Select
              mode="multiple"
              value={presetState.weekdays}
              disabled={disabled}
              onChange={(v) => emitPreset({ ...presetState, weekdays: v })}
              placeholder={t('ds.cron.weekdayLabel')}
              style={{ minWidth: 180 }}
              options={ALL_WEEKDAYS.map((w) => ({ value: w, label: t('ds.cron.weekday' + w) }))}
            />
            {renderTimePair()}
          </>
        )
      case 'monthly':
        return (
          <>
            <Select
              mode="multiple"
              value={presetState.days}
              disabled={disabled}
              onChange={(v) => emitPreset({ ...presetState, days: v })}
              placeholder={t('ds.cron.dayLabel')}
              style={{ minWidth: 140 }}
              options={ALL_DAYS.map((d) => ({
                value: d,
                label: String(d),
                // 北京时间 0~7 点等西向借位场景，1 日跨月界无法精确换算
                disabled: d === 1 && monthlyBorrow,
              }))}
            />
            {renderTimePair()}
          </>
        )
    }
  }

  const selectEmpty =
    (presetState.type === 'weekly' && presetState.weekdays.length === 0) ||
    (presetState.type === 'monthly' && presetState.days.length === 0)

  return (
    <div style={{ width: '100%', display: 'flex', flexDirection: 'column', gap: token.marginXS, ...style }}>
      <div>
        <Segmented
          size="small"
          value={tab}
          disabled={disabled}
          onChange={(v) => {
            const next = v as 'preset' | 'custom'
            if (next === 'custom') {
              // 切自定义时带回当前表达式初值，避免从空开始
              setFields(splitCron(value ?? '') ?? fields)
            }
            setTab(next)
          }}
          options={[
            { value: 'preset', label: t('ds.cron.presetTab') },
            { value: 'custom', label: t('ds.cron.customTab') },
          ]}
        />
      </div>
      {tab === 'preset' ? (
        <div style={{ display: 'flex', alignItems: 'center', gap: spacing.xs, flexWrap: 'wrap', minHeight: 24 }}>
          <Select
            value={presetState.type}
            disabled={disabled}
            onChange={(v) => emitPreset({ ...presetState, type: v as CronUIFreqType })}
            style={{ width: 120 }}
            options={freqOptions}
          />
          {renderPresetControls()}
          {selectEmpty && (
            <Text type="danger" style={{ fontSize: 12 }}>
              {t('ds.cron.selectRequired')}
            </Text>
          )}
        </div>
      ) : (
        <div>
          <div style={{ display: 'flex', gap: spacing.xs, flexWrap: 'wrap' }}>
            {CRON_FIELD_KEYS.map((k) => (
              <div key={k} style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 2 }}>
                <Input
                  size="small"
                  style={{ width: 72, textAlign: 'center' }}
                  value={fields[k]}
                  disabled={disabled}
                  status={isValidField(k, fields[k]) ? undefined : 'error'}
                  placeholder={t(FIELD_LABEL_KEY[k])}
                  onChange={(e) => setField(k, e.target.value)}
                />
                <Text type="secondary" style={{ fontSize: 12 }}>
                  {t(FIELD_LABEL_KEY[k])}
                </Text>
              </div>
            ))}
          </div>
          {customInvalid && (
            <Text type="danger" style={{ fontSize: 12 }}>
              {t('ds.cron.invalid')}
            </Text>
          )}
        </div>
      )}
      <div
        style={{
          background: token.colorFillTertiary,
          borderRadius: token.borderRadiusSM,
          padding: `${token.paddingXS}px ${token.paddingSM}px`,
          display: 'flex',
          flexDirection: 'column',
          gap: 2,
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: spacing.xs, flexWrap: 'wrap' }}>
          <Text type="secondary" style={{ fontSize: 12 }}>
            {t('ds.cron.expression')}：
          </Text>
          <Text code style={{ fontSize: 12 }}>
            {value || '-'}
          </Text>
          {descText && (
            <Text type="secondary" style={{ fontSize: 12 }}>
              {descText}
            </Text>
          )}
        </div>
        {invalid ? (
          <Text type="danger" style={{ fontSize: 12 }}>
            {t('ds.cron.invalid')}
          </Text>
        ) : (
          nextRun && (
            <Text type="secondary" style={{ fontSize: 12 }}>
              {t('ds.cron.nextRun')}：{formatInOffset(nextRun, displayOffsetMinutes)}（{tzLabel(displayOffsetMinutes)}）
            </Text>
          )
        )}
      </div>
    </div>
  )
}

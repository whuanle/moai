import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import {
  MessageOutlined,
  SendOutlined,
  TeamOutlined,
  ThunderboltOutlined,
  DatabaseOutlined,
} from '@ant-design/icons'
import { Card, Col, Row, Typography, theme } from 'antd'
import { useTranslation } from 'react-i18next'
import { chartColors, StatCard } from '@/design-system'
import { spacing } from '@/design-system/theme'
import {
  getAppUsage,
  getAppUsageTrend,
  type AppUsageActivityStats,
  type AppUsageModelItem,
  type AppUsageModelTokenSeries,
  type AppUsageSummary,
  type AppUsageTrendItem,
} from '@/api/app'
import { getAppMemoryStats } from '@/api/memory'
import { formatDateTime } from '@/utils/datetime'

export interface AppMonitorSectionProps {
  appId: string
}

/**
 * 应用使用分析分区（浅色指标卡版）：Tokens 四卡（合计/输入/输出/缓存命中率环）
 * + 活动统计 StatCard 行 + 模型用量芯片卡 + 近 30 天趋势双轴图。
 * tokens/调用次数聚合走 Redis 1 分钟缓存（最多滞后约 1 分钟）；
 * 消息总数由统计模块增量维护；趋势来自统计日表。仅团队 Admin+ 访问（入口由 AppWorkspace 控制）。
 */
const { Text } = Typography

/**
 * 从令牌色值派生半透明色（#rgb/#rrggbb → rgba）。
 * 不用 color-mix：jsdom 不解析，页面里透明度叠加全部经此函数取令牌色。
 */
function withAlpha(color: string, alpha: number): string {
  const hex = color.trim()
  const full =
    /^#([0-9a-f]{6})$/i.test(hex)
      ? hex
      : /^#([0-9a-f]{3})$/i.test(hex)
        ? `#${hex[1]}${hex[1]}${hex[2]}${hex[2]}${hex[3]}${hex[3]}`
        : null
  if (!full) {
    return color
  }
  const value = Number.parseInt(full.slice(1), 16)
  const r = (value >> 16) & 0xff
  const g = (value >> 8) & 0xff
  const b = value & 0xff
  return `rgba(${r}, ${g}, ${b}, ${alpha})`
}

/** 活动统计 StatCard 的图标与配色（取设计系统图表色板，按位循环） */
const ACTIVITY_TILES = [
  { key: 'useCount', icon: <ThunderboltOutlined />, accent: chartColors[0] },
  { key: 'sessionCount', icon: <MessageOutlined />, accent: chartColors[1] },
  { key: 'userCount', icon: <TeamOutlined />, accent: chartColors[2] },
  { key: 'messageCount', icon: <SendOutlined />, accent: chartColors[3] },
  { key: 'memoryCount', icon: <DatabaseOutlined />, accent: chartColors[4] },
] as const

export function AppMonitorSection({ appId }: AppMonitorSectionProps) {
  const { t, i18n } = useTranslation()
  const { token } = theme.useToken()
  const formatInt = useCallback(
    (value?: number | null) => new Intl.NumberFormat(i18n.language).format(Number(value ?? 0)),
    [i18n.language],
  )
  const [loading, setLoading] = useState(true)
  const [summary, setSummary] = useState<AppUsageSummary>({
    callCount: 0,
    promptTokens: 0,
    completionTokens: 0,
    totalTokens: 0,
    cachedTokens: 0,
  })
  const [byModel, setByModel] = useState<AppUsageModelItem[]>([])
  const [stats, setStats] = useState<AppUsageActivityStats>({
    useCount: 0,
    sessionCount: 0,
    userCount: 0,
    messageCount: 0,
    lastActiveTime: null,
  })
  const [memoryCount, setMemoryCount] = useState(0)
  const [trend, setTrend] = useState<AppUsageTrendItem[]>([])
  const [modelTokenSeries, setModelTokenSeries] = useState<AppUsageModelTokenSeries[]>([])

  useEffect(() => {
    if (!appId) return
    let cancelled = false
    setLoading(true)
    getAppUsage(appId)
      .then((res) => {
        if (cancelled) return
        setSummary(res.summary)
        setByModel(res.byModel ?? [])
        setStats(res.stats)
      })
      .catch(() => {
        if (cancelled) return
        setSummary({ callCount: 0, promptTokens: 0, completionTokens: 0, totalTokens: 0, cachedTokens: 0 })
        setByModel([])
        setStats({ useCount: 0, sessionCount: 0, userCount: 0, messageCount: 0, lastActiveTime: null })
      })
      .finally(() => {
        if (!cancelled) setLoading(false)
      })
    // 记忆条数来自记忆统计接口（应用内全部用户），失败不影响用量展示
    getAppMemoryStats(appId)
      .then((res) => {
        if (!cancelled) setMemoryCount(res.totalMemories)
      })
      .catch(() => undefined)
    // 趋势来自统计日表，失败降级为空态展示
    getAppUsageTrend(appId, 30)
      .then((res) => {
        if (!cancelled) {
          setTrend(res.items ?? [])
          setModelTokenSeries(res.modelTokenSeries ?? [])
        }
      })
      .catch(() => {
        if (!cancelled) {
          setTrend([])
          setModelTokenSeries([])
        }
      })
    return () => {
      cancelled = true
    }
  }, [appId])

  const totalTokens = summary.totalTokens ?? 0
  // 缓存命中率按占输入 token 计算（与明细同口径）
  const cachedRate =
    (summary.promptTokens ?? 0) > 0
      ? Math.min(100, Math.round(((summary.cachedTokens ?? 0) / (summary.promptTokens ?? 0)) * 100))
      : 0
  const modelTotalTokens = useMemo(
    () => byModel.reduce((sum, m) => sum + (m.totalTokens ?? 0), 0),
    [byModel],
  )
  const hasTrendData = useMemo(() => trend.some((x) => (x.useCount ?? 0) > 0 || (x.messageCount ?? 0) > 0), [trend])
  // 模型 token 序列按芯片卡顺序排列，折线调色板与芯片色点一一对应
  const orderedModelTokenSeries = useMemo(() => {
    const chipIndex = new Map(byModel.map((m, i) => [String(m.modelId ?? ''), i]))
    return [...modelTokenSeries].sort(
      (a, b) =>
        (chipIndex.get(String(a.modelId ?? '')) ?? Number.MAX_SAFE_INTEGER) -
        (chipIndex.get(String(b.modelId ?? '')) ?? Number.MAX_SAFE_INTEGER),
    )
  }, [byModel, modelTokenSeries])

  const tileValues: Record<(typeof ACTIVITY_TILES)[number]['key'], number> = {
    useCount: stats.useCount,
    sessionCount: stats.sessionCount,
    userCount: stats.userCount,
    messageCount: stats.messageCount,
    memoryCount,
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: spacing.md }}>
      {/* Tokens 指标卡行：合计（大数 + 最近使用）/ 输入 / 输出 / 缓存命中率环 */}
      <Row gutter={[spacing.md, spacing.md]}>
        <Col xs={24} sm={12} lg={6}>
          <Card loading={loading} styles={{ body: { padding: spacing.md, height: '100%' } }}>
            <Text type="secondary" style={{ fontSize: 13 }}>
              {t('appMonitor.totalTokens')}
            </Text>
            <div style={{ marginTop: spacing.xs, fontSize: 30, fontWeight: 700, lineHeight: 1.3 }}>
              {formatInt(totalTokens)}
            </div>
            <div style={{ marginTop: spacing.xs, fontSize: 12, color: token.colorTextTertiary }}>
              {stats.lastActiveTime
                ? t('appMonitor.lastActive', { time: formatDateTime(stats.lastActiveTime) })
                : '—'}
            </div>
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card loading={loading} styles={{ body: { padding: spacing.md, height: '100%' } }}>
            <Text type="secondary" style={{ fontSize: 13 }}>
              {t('appMonitor.promptTokens')}
            </Text>
            <div style={{ marginTop: spacing.xs, fontSize: 30, fontWeight: 700, lineHeight: 1.3 }}>
              {formatInt(summary.promptTokens)}
            </div>
            <div style={{ marginTop: spacing.xs, fontSize: 12, color: token.colorTextTertiary }}>
              {t('appMonitor.cachedTokens')} {formatInt(summary.cachedTokens)}
            </div>
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card loading={loading} styles={{ body: { padding: spacing.md, height: '100%' } }}>
            <Text type="secondary" style={{ fontSize: 13 }}>
              {t('appMonitor.completionTokens')}
            </Text>
            <div style={{ marginTop: spacing.xs, fontSize: 30, fontWeight: 700, lineHeight: 1.3 }}>
              {formatInt(summary.completionTokens)}
            </div>
            <div style={{ marginTop: spacing.xs, fontSize: 12, color: token.colorTextTertiary }}>
              {t('appMonitor.callCount')} {formatInt(summary.callCount)}
            </div>
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card loading={loading} styles={{ body: { padding: spacing.md, height: '100%' } }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: spacing.md }}>
              <CachedRateRing rate={cachedRate} loading={loading} />
              <div style={{ minWidth: 0 }}>
                <Text type="secondary" style={{ fontSize: 13 }}>
                  {t('appMonitor.cachedRate')}
                </Text>
                <div style={{ fontSize: 22, fontWeight: 700, lineHeight: 1.3 }}>{cachedRate}%</div>
                <div style={{ fontSize: 12, color: token.colorTextTertiary }}>
                  {formatInt(summary.cachedTokens)} / {formatInt(summary.promptTokens)}
                </div>
              </div>
            </div>
          </Card>
        </Col>
      </Row>

      {/* 活动统计指标卡行 */}
      <Row gutter={[spacing.md, spacing.md]}>
        {ACTIVITY_TILES.map((tile) => (
          <Col key={tile.key} xs={24} sm={12} md={8} lg={4}>
            <StatCard
              loading={loading}
              title={t(`appMonitor.${tile.key}`)}
              value={formatInt(tileValues[tile.key])}
              icon={<span style={{ color: tile.accent }}>{tile.icon}</span>}
            />
          </Col>
        ))}
      </Row>

      {/* 模型用量：芯片卡片（每个模型一张，无表格无进度条） */}
      <Card title={t('appMonitor.byModelTitle')} loading={loading}>
        {byModel.length === 0 ? (
          <div style={{ padding: `${spacing.lg}px 0`, textAlign: 'center' }}>
            <Text type="secondary">{t('appMonitor.empty')}</Text>
          </div>
        ) : (
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: spacing.md }}>
            {byModel.map((item, index) => {
              const accent = chartColors[index % chartColors.length]
              const tokens = item.totalTokens ?? 0
              const hasUsage = tokens > 0 || (item.callCount ?? 0) > 0
              const share = modelTotalTokens > 0 ? Math.round((tokens / modelTotalTokens) * 100) : 0
              return (
                <div
                  key={item.modelId ?? index}
                  style={{
                    flex: '1 1 300px',
                    minWidth: 260,
                    maxWidth: '100%',
                    padding: spacing.md,
                    borderRadius: token.borderRadiusLG,
                    border: `1px ${hasUsage ? 'solid' : 'dashed'} ${hasUsage ? token.colorBorderSecondary : token.colorBorderSecondary}`,
                    background: hasUsage ? token.colorBgContainer : token.colorFillQuaternary,
                  }}
                >
                  <div style={{ display: 'flex', alignItems: 'center', gap: spacing.sm }}>
                    <span
                      aria-hidden
                      style={{
                        width: 10,
                        height: 10,
                        borderRadius: '50%',
                        background: accent,
                        flexShrink: 0,
                      }}
                    />
                    <Text strong ellipsis style={{ flex: 1, minWidth: 0 }}>
                      {item.modelName || item.modelId || '-'}
                    </Text>
                    {item.isDefault && (
                      <Text
                        style={{
                          fontSize: 12,
                          whiteSpace: 'nowrap',
                          padding: `0 ${spacing.xs}px`,
                          borderRadius: token.borderRadiusSM,
                          background: token.colorPrimaryBg,
                          color: token.colorPrimary,
                        }}
                      >
                        {t('appMonitor.modelDefaultBadge')}
                      </Text>
                    )}
                    {hasUsage && (
                      <Text
                        type="secondary"
                        style={{
                          fontSize: 12,
                          whiteSpace: 'nowrap',
                          padding: `0 ${spacing.xs}px`,
                          borderRadius: token.borderRadiusSM,
                          background: withAlpha(accent, 0.1),
                        }}
                      >
                        {share}%
                      </Text>
                    )}
                  </div>
                  <div style={{ marginTop: spacing.sm, fontSize: 22, fontWeight: 600, lineHeight: 1.3 }}>
                    {formatInt(tokens)}
                    <span style={{ marginInlineStart: spacing.xs, fontSize: 13, fontWeight: 400, color: token.colorTextTertiary }}>
                      {t('appMonitor.tokensUnit')}
                    </span>
                  </div>
                  <Text type="secondary" style={{ display: 'block', marginTop: spacing.xs, fontSize: 12 }}>
                    {hasUsage
                      ? t('appMonitor.modelMeta', {
                          calls: formatInt(item.callCount),
                          prompt: formatInt(item.promptTokens),
                          completion: formatInt(item.completionTokens),
                          cached: formatInt(item.cachedTokens),
                        })
                      : t('appMonitor.modelNoUsage')}
                  </Text>
                </div>
              )
            })}
          </div>
        )}
      </Card>

      {/* 近 30 天趋势：使用次数（柱）+ 消息条数（线）+ 各模型 token 多折线，来自统计日表 */}
      <Card title={t('appMonitor.trendTitle')} loading={loading}>
        {trend.length === 0 || !hasTrendData ? (
          <div style={{ padding: `${spacing.lg}px 0`, textAlign: 'center' }}>
            <Text type="secondary">{t('appMonitor.trendEmpty')}</Text>
          </div>
        ) : (
          <UsageTrendChart items={trend} />
        )}
        {modelTokenSeries.length > 0 && hasTrendData && (
          <>
            <div style={{ marginTop: trend.length > 0 && hasTrendData ? spacing.lg : 0 }}>
              <Text type="secondary" strong style={{ fontSize: 13 }}>
                {t('appMonitor.trendModelTitle')}
              </Text>
            </div>
            <ModelTokenTrendChart series={orderedModelTokenSeries} />
          </>
        )}
      </Card>
    </div>
  )
}

/** 浅色缓存命中率环：conic-gradient 扇区 + radial mask 挖环心 */
function CachedRateRing({ rate, loading }: { rate: number; loading: boolean }) {
  const { token } = theme.useToken()
  if (loading) {
    return <div style={{ width: 60, height: 60 }} />
  }
  const track = token.colorFillSecondary ?? token.colorFill
  return (
    <div
      style={{
        position: 'relative',
        width: 60,
        height: 60,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        flexShrink: 0,
      }}
    >
      <div
        style={{
          position: 'absolute',
          inset: 0,
          borderRadius: '50%',
          background: `conic-gradient(${token.colorPrimary} ${rate}%, ${track} 0)`,
          WebkitMask: `radial-gradient(farthest-side, transparent calc(100% - 7px), #000 calc(100% - 7px))`,
          mask: `radial-gradient(farthest-side, transparent calc(100% - 7px), #000 calc(100% - 7px))`,
        }}
      />
      <div style={{ fontSize: 13, fontWeight: 600 }}>{rate}%</div>
    </div>
  )
}

/**
 * gpt-vis 动态加载（单飞）：页内两张趋势图共享同一 in-flight import —— 其 CJS 构建
 * （type:module）无法被测试环境静态解析，且并发重复动态 import 会绕过测试 mock；
 * 单飞同时拆出首屏外 chunk。加载失败由各图表自行降级.
 */
let gptVisModules: Promise<typeof import('@antv/gpt-vis')> | null = null
function loadGptVis(): Promise<typeof import('@antv/gpt-vis')> {
  gptVisModules ??= import('@antv/gpt-vis')
  return gptVisModules
}

/**
 * 近 30 天趋势双轴图（gpt-vis DualAxes）：使用次数柱 + 消息条数线，实例与容器同生命周期，
 * 数据变化整体重渲染。gpt-vis 经动态 import 引入（其 CJS 构建无法被测试环境静态解析，
 * 且可拆出首屏外 chunk）；加载/渲染失败降级为空态文案，不阻塞页面.
 */
function UsageTrendChart({ items }: { items: AppUsageTrendItem[] }) {
  const { t, i18n } = useTranslation()
  const containerRef = useRef<HTMLDivElement | null>(null)
  const [failed, setFailed] = useState(false)

  useEffect(() => {
    const el = containerRef.current
    if (!el || items.length === 0) {
      return undefined
    }
    let disposed = false
    let instance: { render: (config: never) => void; destroy: () => void } | null = null
    void (async () => {
      try {
        const { DualAxes } = await loadGptVis()
        if (disposed || !containerRef.current) {
          return
        }
        instance = DualAxes({
          container: containerRef.current,
          height: 280,
          theme: 'light',
          locale: i18n.language,
        })
        instance.render({
          categories: items.map((x) => (x.date ?? '').slice(5)),
          series: [
            { type: 'column', data: items.map((x) => x.useCount ?? 0), axisYTitle: t('appMonitor.trendUseCount') },
            { type: 'line', data: items.map((x) => x.messageCount ?? 0), axisYTitle: t('appMonitor.trendMessageCount') },
          ],
        } as never)
        if (!disposed) {
          setFailed(false)
        }
      } catch {
        if (!disposed) {
          setFailed(true)
        }
      }
    })()
    return () => {
      disposed = true
      instance?.destroy()
      instance = null
    }
  }, [items, t, i18n.language])

  if (failed) {
    return (
      <div style={{ padding: `${spacing.lg}px 0`, textAlign: 'center' }}>
        <Typography.Text type="secondary">{t('appMonitor.trendEmpty')}</Typography.Text>
      </div>
    )
  }
  return <div ref={containerRef} style={{ width: '100%', height: 280 }} />
}

/**
 * 各模型 Token 消耗趋势分组折线（gpt-vis Line）：每模型一条线（server 已按区间合计降序取 top 8），
 * 调色板传设计系统图表色板并与芯片卡色点对齐（series 已按芯片顺序排列）；
 * 经动态 import 引入（同 DualAxes：CJS 构建无法被测试环境静态解析），加载/渲染失败降级为空态文案.
 */
function ModelTokenTrendChart({ series }: { series: AppUsageModelTokenSeries[] }) {
  const { t, i18n } = useTranslation()
  const containerRef = useRef<HTMLDivElement | null>(null)
  const [failed, setFailed] = useState(false)

  useEffect(() => {
    const el = containerRef.current
    if (!el || series.length === 0) {
      return undefined
    }
    let disposed = false
    let instance: { render: (config: never) => void; destroy: () => void } | null = null
    void (async () => {
      try {
        const { Line } = await loadGptVis()
        if (disposed || !containerRef.current) {
          return
        }
        instance = Line({
          container: containerRef.current,
          height: 240,
          theme: 'light',
          locale: i18n.language,
        })
        instance.render({
          data: series.flatMap((s) =>
            s.points.map((p) => ({
              time: (p.date ?? '').slice(5),
              value: p.tokens,
              group: s.modelName || s.modelId || '-',
            })),
          ),
          axisYTitle: t('appMonitor.totalTokens'),
          style: { palette: [...chartColors] },
        } as never)
        if (!disposed) {
          setFailed(false)
        }
      } catch {
        if (!disposed) {
          setFailed(true)
        }
      }
    })()
    return () => {
      disposed = true
      instance?.destroy()
      instance = null
    }
  }, [series, t, i18n.language])

  if (failed) {
    return (
      <div style={{ padding: `${spacing.lg}px 0`, textAlign: 'center' }}>
        <Typography.Text type="secondary">{t('appMonitor.trendModelEmpty')}</Typography.Text>
      </div>
    )
  }
  return <div ref={containerRef} style={{ width: '100%', height: 240 }} />
}

import { useCallback, useEffect, useRef, useState } from 'react'
import { Button, Divider, Form, Input, InputNumber, Popconfirm, Row, Col, Switch, Tag, Typography } from 'antd'
import { DeleteOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import { feedback } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { getAppAgentConfig, getMemoryDefaults } from '@/api/app'
import { clearAppMemory, getAppMemoryStats, getMyMemories, type MyMemoryItem } from '@/api/memory'
import { formatDateTime } from '@/utils/datetime'

const { Text } = Typography

/** 记忆类型常量（对齐后端 AgentMemorySettings.Type*） */
const TYPE_EPISODIC = 1
const TYPE_PROCEDURAL = 2

export interface AppMemorySettingsPanelProps {
  teamId: number
  appId: string
  canManage: boolean
  /**
   * 宿主统一保存回调注册（Agent 应用「配置」分区嵌入态）：传入后面板不渲染保存按钮、不自行提示成功，
   * 由宿主页「保存设置」把 memory 节并入主配置一次提交（随发布快照生效）；
   * 校验失败（平台未开启长期记忆）提示并抛错中断宿主整体保存。
   * 注册在配置加载完成后才生效，避免宿主在回显前保存到默认值。
   */
  registerSettings?: (get: (() => Record<string, unknown>) | null) => void
}

/**
 * 长期记忆设置面板（「配置」分区收缩卡片承载）：长期记忆总开关级联
 * （记忆提示词、自动提取、检索参数、单用户容量；记忆类型由 AI 自行判定，不提供限制）；
 * 向量化模型/维度统一由系统设置（MEMORY_*）决定，应用侧只读回显、不可自选；
 * 记忆提示词追加为记忆工具描述（指导 AI 如何管理与使用记忆），留空用平台默认（占位符展示）；
 * memory 节经 registerSettings 注册给宿主页，随「保存设置」并入 executionSettings 一次提交；
 * 下方维护应用记忆统计、管理员本人记忆样例与「清空应用全部记忆」危险区（即时接口，不经发布快照）；
 * 用户的个人记忆偏好（模式/清空）在对话页应用设置中调整。
 */
export function AppMemorySettingsPanel({ appId, canManage, registerSettings }: AppMemorySettingsPanelProps) {
  const { t } = useTranslation()

  const [loading, setLoading] = useState(true)
  const [memoryEnabled, setMemoryEnabled] = useState(false)
  const [allowGlobalMemory, setAllowGlobalMemory] = useState(false)
  const [prompt, setPrompt] = useState('')
  // 平台默认记忆提示词（prompt 留空时生效，展示为输入框占位符）
  const [defaultPrompt, setDefaultPrompt] = useState('')
  // 平台统一记忆设置回显（系统设置 MEMORY_*：应用记忆与全局记忆共用，应用侧不可自选）
  const [platformEnabled, setPlatformEnabled] = useState(true)
  const [platformModelName, setPlatformModelName] = useState('')
  const [platformDimensions, setPlatformDimensions] = useState(1024)
  const [autoExtract, setAutoExtract] = useState(true)
  const [retrievalTopK, setRetrievalTopK] = useState<number | null>(null)
  const [maxPerUser, setMaxPerUser] = useState<number | null>(null)
  // 应用内本人记忆样例（帮助管理员确认记忆在生效；只读）
  const [myMemories, setMyMemories] = useState<MyMemoryItem[]>([])
  const [myMemoryTotal, setMyMemoryTotal] = useState(0)
  // 应用维度记忆统计（全部用户）
  const [stats, setStats] = useState<{ totalMemories: number; userCount: number } | null>(null)

  const load = useCallback(async () => {
    if (!appId) return
    setLoading(true)
    try {
      const [configRow, defaults] = await Promise.all([
        getAppAgentConfig(appId),
        getMemoryDefaults(),
      ])
      const memory = ((configRow.executionSettings ?? {}).memory ?? {}) as Record<string, unknown>
      setMemoryEnabled(memory.enabled === true)
      setAllowGlobalMemory(memory.allowGlobalMemory === true)
      setPrompt(typeof memory.prompt === 'string' ? (memory.prompt as string) : '')
      setDefaultPrompt(defaults.prompt)
      setPlatformEnabled(defaults.platformEnabled)
      setPlatformModelName(defaults.embeddingModelName)
      setPlatformDimensions(defaults.embeddingDimensions)
      setAutoExtract(memory.autoExtract !== false)
      setRetrievalTopK(typeof memory.retrievalTopK === 'number' ? (memory.retrievalTopK as number) : null)
      setMaxPerUser(typeof memory.maxPerUser === 'number' ? (memory.maxPerUser as number) : null)
      try {
        const mine = await getMyMemories(appId, 1, 5)
        setMyMemories(mine.items)
        setMyMemoryTotal(mine.total)
      } catch {
        // 记忆接口失败不影响配置编辑
      }
      try {
        setStats(await getAppMemoryStats(appId))
      } catch {
        // 统计失败不影响配置编辑
      }
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoading(false)
    }
  }, [appId])

  useEffect(() => {
    void load()
  }, [load])

  /** 构造 memory 节（宿主保存时并入 executionSettings）；向量化模型/维度由系统设置统一决定，不在此回传 */
  const buildMemorySettings = (): Record<string, unknown> => {
    return {
      enabled: memoryEnabled,
      // 记忆提示词：非空才回传（空=后端按平台默认生效）
      ...(prompt.trim() ? { prompt: prompt.trim() } : {}),
      ...(memoryEnabled
        ? {
            // 允许用户接入全局记忆：可随保存自由切换（全局记忆同样受系统设置总开关管控）
            allowGlobalMemory,
            autoExtract,
            ...(retrievalTopK != null && retrievalTopK > 0 ? { retrievalTopK } : {}),
            ...(maxPerUser != null && maxPerUser >= 10 ? { maxPerUser } : {}),
          }
        : {}),
    }
  }

  const settingsRef = useRef<() => Record<string, unknown>>(() => ({}))
  settingsRef.current = buildMemorySettings

  // 嵌入态：配置加载完成后才注册 memory 节构造回调（加载中不注册，防宿主在回显前把默认值存库）；卸载/换应用时注销
  useEffect(() => {
    if (!registerSettings || loading) return
    registerSettings(() => settingsRef.current())
    return () => registerSettings(null)
  }, [registerSettings, loading])

  const handleClearAppMemory = async () => {
    if (!appId) return
    try {
      await clearAppMemory(appId)
      feedback.success(t('appMemory.clearAppSuccess'))
      setMyMemories([])
      setMyMemoryTotal(0)
      setStats({ totalMemories: 0, userCount: 0 })
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const typeName = (type: number) =>
    type === TYPE_EPISODIC ? t('appMemory.typeEpisodic') : type === TYPE_PROCEDURAL ? t('appMemory.typeProcedural') : t('appMemory.typeSemantic')

  if (loading) {
    return null
  }

  return (
    <div>
      <Form layout="vertical" disabled={!canManage}>
        <Form.Item
          label={t('appMemory.enabled')}
          valuePropName="checked"
          extra={platformEnabled ? t('appMemory.enabledHint') : t('appMemory.platformOffHint')}
          style={{ marginBottom: spacing.sm }}
        >
          <Switch
            checked={memoryEnabled}
            disabled={!platformEnabled}
            onChange={(checked) => setMemoryEnabled(checked)}
          />
        </Form.Item>
        {memoryEnabled && (
          <>
            <Form.Item label={t('appMemory.platformModelLabel')} extra={t('appMemory.platformModelHint')} style={{ marginBottom: spacing.sm }}>
              <Text strong>{platformModelName || t('appMemory.platformModelEmpty')}</Text>
              <Text type="secondary" style={{ marginLeft: spacing.sm }}>
                {t('appMemory.platformDimensions', { dims: platformDimensions })}
              </Text>
            </Form.Item>
            <Form.Item label={t('appMemory.prompt')} extra={t('appMemory.promptHint')} style={{ marginBottom: spacing.sm }}>
              <Input.TextArea
                value={prompt}
                onChange={(e) => setPrompt(e.target.value)}
                placeholder={defaultPrompt}
                maxLength={2000}
                autoSize={{ minRows: 4, maxRows: 12 }}
              />
            </Form.Item>
            <Form.Item label={t('appMemory.autoExtract')} valuePropName="checked" extra={t('appMemory.autoExtractHint')} style={{ marginBottom: spacing.sm }}>
              <Switch checked={autoExtract} onChange={setAutoExtract} />
            </Form.Item>
            <Form.Item label={t('appMemory.allowGlobalMemory')} valuePropName="checked" extra={t('appMemory.allowGlobalMemoryHint')} style={{ marginBottom: spacing.sm }}>
              <Switch checked={allowGlobalMemory} onChange={setAllowGlobalMemory} />
            </Form.Item>
            <Row gutter={spacing.md}>
              <Col xs={24} md={8}>
                <Form.Item label={t('appMemory.retrievalTopK')}>
                  <InputNumber
                    min={1}
                    max={50}
                    precision={0}
                    style={{ width: '100%' }}
                    value={retrievalTopK ?? undefined}
                    onChange={(value) => setRetrievalTopK(typeof value === 'number' ? value : null)}
                    placeholder="5"
                  />
                </Form.Item>
              </Col>
              <Col xs={24} md={8}>
                <Form.Item label={t('appMemory.maxPerUser')}>
                  <InputNumber
                    min={10}
                    max={10000}
                    precision={0}
                    style={{ width: '100%' }}
                    value={maxPerUser ?? undefined}
                    onChange={(value) => setMaxPerUser(typeof value === 'number' ? value : null)}
                    placeholder="500"
                  />
                </Form.Item>
              </Col>
            </Row>
          </>
        )}
      </Form>

      {stats && (
        <>
          <Divider style={{ margin: `${spacing.md}px 0` }} />
          <Text type="secondary">
            {stats.totalMemories > 0
              ? t('appMemory.statsHint', { count: stats.totalMemories, users: stats.userCount })
              : t('appMemory.statsEmpty')}
          </Text>
        </>
      )}

      {myMemoryTotal > 0 && (
        <>
          <Text type="secondary">{t('appMemory.myMemoriesHint', { count: myMemoryTotal })}</Text>
          <div style={{ marginTop: spacing.xs, display: 'flex', flexDirection: 'column', gap: spacing.xs }}>
            {myMemories.map((item) => (
              <div key={item.id} style={{ display: 'flex', alignItems: 'baseline', gap: spacing.sm }}>
                <Tag>{typeName(item.memoryType ?? 0)}</Tag>
                <Text style={{ flex: 1, minWidth: 0 }} ellipsis>
                  {item.content}
                </Text>
                <Text type="secondary" style={{ flexShrink: 0 }}>
                  {formatDateTime(item.updateTime)}
                </Text>
              </div>
            ))}
          </div>
        </>
      )}

      {canManage && (
        <>
          <Divider style={{ margin: `${spacing.md}px 0` }} />
          <Popconfirm title={t('appMemory.clearAppConfirm')} onConfirm={() => void handleClearAppMemory()}>
            <Button danger icon={<DeleteOutlined />}>
              {t('appMemory.clearApp')}
            </Button>
          </Popconfirm>
          <div style={{ marginTop: spacing.xs }}>
            <Text type="secondary">{t('appMemory.clearAppHint')}</Text>
          </div>
        </>
      )}
    </div>
  )
}

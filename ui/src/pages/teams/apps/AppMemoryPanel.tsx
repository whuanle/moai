import { useCallback, useEffect, useRef, useState } from 'react'
import { Button, Col, Divider, Form, Input, InputNumber, Popconfirm, Row, Select, Switch, Tag, Typography } from 'antd'
import { DeleteOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import { feedback } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { getAppAgentConfig, getMemoryDefaults } from '@/api/app'
import { getWikiModelOptions } from '@/api/wiki'
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
   * 校验失败（开启未选向量化模型）提示并抛错中断宿主整体保存。
   * 注册在配置加载完成后才生效，避免宿主在回显前保存到默认值。
   */
  registerSettings?: (get: (() => Record<string, unknown>) | null) => void
}

/**
 * 长期记忆设置面板（「配置」分区收缩卡片承载）：长期记忆总开关级联
 * （向量化模型/维度、记忆提示词、自动提取、检索参数、单用户容量；记忆类型由 AI 自行判定，不提供限制）；
 * 记忆提示词追加为记忆工具描述（指导 AI 如何管理与使用记忆），留空用平台默认（占位符展示）；
 * memory 节经 registerSettings 注册给宿主页，随「保存设置」并入 executionSettings 一次提交；
 * 下方维护应用记忆统计、管理员本人记忆样例与「清空应用全部记忆」危险区（即时接口，不经发布快照）；
 * 用户的个人记忆偏好（模式/清空）在对话页应用设置中调整。
 */
export function AppMemorySettingsPanel({ teamId, appId, canManage, registerSettings }: AppMemorySettingsPanelProps) {
  const { t } = useTranslation()

  const [loading, setLoading] = useState(true)
  const [memoryEnabled, setMemoryEnabled] = useState(false)
  const [embeddingModelId, setEmbeddingModelId] = useState<string>()
  const [embeddingDimensions, setEmbeddingDimensions] = useState<number | null>(null)
  const [prompt, setPrompt] = useState('')
  // 平台默认记忆提示词（prompt 留空时生效，展示为输入框占位符）
  const [defaultPrompt, setDefaultPrompt] = useState('')
  const [autoExtract, setAutoExtract] = useState(true)
  const [retrievalTopK, setRetrievalTopK] = useState<number | null>(null)
  const [maxPerUser, setMaxPerUser] = useState<number | null>(null)
  const [embeddingModels, setEmbeddingModels] = useState<{ value: string; label: string }[]>([])
  // 应用内本人记忆样例（帮助管理员确认记忆在生效；只读）
  const [myMemories, setMyMemories] = useState<MyMemoryItem[]>([])
  const [myMemoryTotal, setMyMemoryTotal] = useState(0)
  // 应用维度记忆统计（全部用户）；已有记忆时向量化模型与维度锁定（不同模型/维度的向量不可比，需先清空才能换）
  const [stats, setStats] = useState<{ totalMemories: number; userCount: number } | null>(null)

  const load = useCallback(async () => {
    if (!appId) return
    setLoading(true)
    try {
      const [configRow, models, defaults] = await Promise.all([
        getAppAgentConfig(appId),
        getWikiModelOptions(teamId),
        getMemoryDefaults(),
      ])
      const memory = ((configRow.executionSettings ?? {}).memory ?? {}) as Record<string, unknown>
      setMemoryEnabled(memory.enabled === true)
      setEmbeddingModelId(typeof memory.embeddingModelId === 'string' && memory.embeddingModelId ? memory.embeddingModelId : undefined)
      setEmbeddingDimensions(typeof memory.embeddingDimensions === 'number' ? (memory.embeddingDimensions as number) : null)
      setPrompt(typeof memory.prompt === 'string' ? (memory.prompt as string) : '')
      setDefaultPrompt(defaults.prompt)
      setAutoExtract(memory.autoExtract !== false)
      setRetrievalTopK(typeof memory.retrievalTopK === 'number' ? (memory.retrievalTopK as number) : null)
      setMaxPerUser(typeof memory.maxPerUser === 'number' ? (memory.maxPerUser as number) : null)
      setEmbeddingModels(
        (models?.embeddingModels ?? [])
          .filter((item) => item.id)
          .map((item) => ({ value: String(item.id), label: item.name || '-' })),
      )
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
  }, [appId, teamId])

  useEffect(() => {
    void load()
  }, [load])

  /** 构造 memory 节（宿主保存时并入 executionSettings）；开启未选向量化模型时提示并抛错中断宿主保存 */
  const buildMemorySettings = (): Record<string, unknown> => {
    if (memoryEnabled && !embeddingModelId) {
      feedback.error(t('appMemory.modelRequired'))
      throw new Error('embedding model required')
    }
    return {
      enabled: memoryEnabled,
      // 模型/维度无论开关都回传：关闭期间保留锁定参照系，重新开启时口径不变
      ...(embeddingModelId ? { embeddingModelId } : {}),
      ...(embeddingDimensions != null && embeddingDimensions > 0 ? { embeddingDimensions } : {}),
      // 记忆提示词：非空才回传（空=后端按平台默认生效）
      ...(prompt.trim() ? { prompt: prompt.trim() } : {}),
      ...(memoryEnabled
        ? {
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

  const memoryLocked = (stats?.totalMemories ?? 0) > 0

  return (
    <div>
      <Form layout="vertical" disabled={!canManage}>
        <Form.Item label={t('appMemory.enabled')} valuePropName="checked" extra={t('appMemory.enabledHint')} style={{ marginBottom: spacing.sm }}>
          <Switch
            checked={memoryEnabled}
            onChange={(checked) => setMemoryEnabled(checked)}
          />
        </Form.Item>
        {memoryEnabled && (
          <>
            <Row gutter={spacing.md}>
              <Col xs={24} md={12}>
                <Form.Item label={t('appMemory.embeddingModel')} extra={t('appMemory.embeddingModelHint')} required>
                  <Select
                    allowClear
                    showSearch
                    optionFilterProp="label"
                    style={{ width: '100%', maxWidth: 360 }}
                    placeholder={t('appMemory.embeddingModelPlaceholder')}
                    value={embeddingModelId}
                    disabled={memoryLocked}
                    onChange={setEmbeddingModelId}
                    options={embeddingModels}
                  />
                </Form.Item>
              </Col>
              <Col xs={24} md={12}>
                <Form.Item label={t('appMemory.embeddingDimensions')} extra={t('appMemory.embeddingDimensionsHint')}>
                  <InputNumber
                    min={1}
                    max={2000}
                    precision={0}
                    style={{ width: '100%' }}
                    value={embeddingDimensions ?? undefined}
                    disabled={memoryLocked}
                    onChange={(value) => setEmbeddingDimensions(typeof value === 'number' ? value : null)}
                    placeholder="1024"
                  />
                </Form.Item>
              </Col>
            </Row>
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

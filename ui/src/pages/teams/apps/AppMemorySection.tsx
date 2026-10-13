import { useCallback, useEffect, useState } from 'react'
import { Alert, Button, Col, Divider, Form, InputNumber, Popconfirm, Row, Select, Space, Switch, Tag, Typography } from 'antd'
import { DeleteOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import { Card as DSCard, feedback } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { getAppAgentConfig, publishApp, saveAppAgentConfig } from '@/api/app'
import { getWikiModelOptions } from '@/api/wiki'
import { clearAppMemory, getAppMemoryStats, getMyMemories, type MyMemoryItem } from '@/api/memory'
import { formatDateTime } from '@/utils/datetime'

const { Text } = Typography

/** 记忆类型常量（对齐后端 AgentMemorySettings.Type*） */
const TYPE_EPISODIC = 1
const TYPE_PROCEDURAL = 2

/** 空模型 id（后端未选择模型时返回全零 Guid） */
const EMPTY_GUID = '00000000-0000-0000-0000-000000000000'

/** 本分区维护的全量配置快照（保存时原样回传，只改 memory 节，避免覆盖其他分区字段） */
interface ConfigSnapshot {
  modelId?: string | null
  prompt: string
  wikiIds: number[]
  graphIds: number[]
  plugins: string[]
  workflowApps: string[]
  skills: string[]
  openingStatement: string
  openingStatementEnabled: boolean
  quickInputs: string[]
  executionSettings: Record<string, unknown>
}

export interface AppMemorySectionProps {
  teamId: number
  appId: string
  canManage: boolean
  isPublished: boolean
  /** 发布动作触发后回报工作台刷新配置状态 */
  onPublished?: () => void
}

/**
 * 应用「记忆」分区：长期记忆总开关级联（向量化模型/维度、记忆类型、自动提取、检索参数、共享范围默认、容量），
 * 危险区提供清空应用全部用户记忆（团队管理员，Popconfirm 二次确认）；
 * 用户的个人记忆偏好（模式/清空）在对话页应用设置中调整.
 */
export function AppMemorySection({ teamId, appId, canManage, isPublished, onPublished }: AppMemorySectionProps) {
  const { t } = useTranslation()

  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [publishing, setPublishing] = useState(false)
  const [memoryEnabled, setMemoryEnabled] = useState(false)
  const [embeddingModelId, setEmbeddingModelId] = useState<string>()
  const [embeddingDimensions, setEmbeddingDimensions] = useState<number | null>(null)
  const [enableSemantic, setEnableSemantic] = useState(true)
  const [enableEpisodic, setEnableEpisodic] = useState(true)
  const [enableProcedural, setEnableProcedural] = useState(true)
  const [autoExtract, setAutoExtract] = useState(true)
  const [retrievalTopK, setRetrievalTopK] = useState<number | null>(null)
  const [maxPerUser, setMaxPerUser] = useState<number | null>(null)
  const [config, setConfig] = useState<ConfigSnapshot | null>(null)
  const [embeddingModels, setEmbeddingModels] = useState<{ value: string; label: string }[]>([])
  // 应用内本人记忆样例（帮助管理员确认记忆在生效；只读）
  const [myMemories, setMyMemories] = useState<MyMemoryItem[]>([])
  const [myMemoryTotal, setMyMemoryTotal] = useState(0)
  // 应用维度记忆统计（全部用户）；已有记忆时向量化模型与维度锁定（不同模型/维度的向量不可比，需先清空才能换）
  const [stats, setStats] = useState<{ totalMemories: number; userCount: number } | null>(null)
  const memoryLocked = (stats?.totalMemories ?? 0) > 0
  const [dirty, setDirty] = useState(false)

  const load = useCallback(async () => {
    if (!appId) return
    setLoading(true)
    try {
      const [configRow, models] = await Promise.all([
        getAppAgentConfig(appId),
        getWikiModelOptions(teamId),
      ])
      const settings = configRow.executionSettings ?? {}
      const memory = (settings.memory ?? {}) as Record<string, unknown>
      setConfig({
        modelId: configRow.modelId && configRow.modelId !== EMPTY_GUID ? configRow.modelId : null,
        prompt: configRow.prompt ?? '',
        wikiIds: configRow.wikiIds ?? [],
        graphIds: configRow.graphIds ?? [],
        plugins: configRow.plugins ?? [],
        workflowApps: configRow.workflowApps ?? [],
        skills: configRow.skills ?? [],
        openingStatement: configRow.openingStatement ?? '',
        openingStatementEnabled: configRow.openingStatementEnabled ?? false,
        quickInputs: configRow.quickInputs ?? [],
        executionSettings: settings,
      })
      setMemoryEnabled(memory.enabled === true)
      setEmbeddingModelId(typeof memory.embeddingModelId === 'string' && memory.embeddingModelId ? memory.embeddingModelId : undefined)
      setEmbeddingDimensions(typeof memory.embeddingDimensions === 'number' ? (memory.embeddingDimensions as number) : null)
      setEnableSemantic(memory.enableSemantic !== false)
      setEnableEpisodic(memory.enableEpisodic !== false)
      setEnableProcedural(memory.enableProcedural !== false)
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

  const buildMemorySettings = (): Record<string, unknown> => ({
    enabled: memoryEnabled,
    // 模型/维度无论开关都回传：关闭期间保留锁定参照系，重新开启时口径不变
    ...(embeddingModelId ? { embeddingModelId } : {}),
    ...(embeddingDimensions != null && embeddingDimensions > 0 ? { embeddingDimensions } : {}),
    ...(memoryEnabled
      ? {
          enableSemantic,
          enableEpisodic,
          enableProcedural,
          autoExtract,
          ...(retrievalTopK != null && retrievalTopK > 0 ? { retrievalTopK } : {}),
          ...(maxPerUser != null && maxPerUser >= 10 ? { maxPerUser } : {}),
        }
      : {}),
  })

  const handleSave = async () => {
    if (!appId || !config) return
    if (memoryEnabled && !embeddingModelId) {
      feedback.error(t('appMemory.modelRequired'))
      return
    }
    setSaving(true)
    try {
      await saveAppAgentConfig(appId, {
        modelId: config.modelId ?? null,
        prompt: config.prompt,
        wikiIds: config.wikiIds,
        graphIds: config.graphIds,
        plugins: config.plugins,
        workflowApps: config.workflowApps,
        skills: config.skills,
        openingStatement: config.openingStatement,
        openingStatementEnabled: config.openingStatementEnabled,
        quickInputs: config.quickInputs,
        executionSettings: { ...config.executionSettings, memory: buildMemorySettings() },
      })
      feedback.success(t(isPublished ? 'appManage.configSaveDraftSuccess' : 'appManage.configSaveSuccess'))
      setDirty(false)
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setSaving(false)
    }
  }

  const handlePublish = async () => {
    if (!appId) return
    setPublishing(true)
    try {
      await publishApp(appId)
      feedback.success(t('appManage.publishSuccess'))
      onPublished?.()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setPublishing(false)
    }
  }

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

  return (
    <DSCard title={t('appMemory.title')} loading={loading}>
      {!canManage && (
        <Alert type="info" showIcon message={t('appManage.memberHint')} style={{ marginBottom: spacing.md }} />
      )}
      {canManage && isPublished && (
        <Alert
          type="warning"
          showIcon
          message={t('appManage.draftPendingHint')}
          action={
            <Button size="small" type="primary" loading={publishing} onClick={() => void handlePublish()}>
              {t('appManage.republish')}
            </Button>
          }
          style={{ marginBottom: spacing.md }}
        />
      )}
      <Form layout="vertical" disabled={!canManage}>
        <Form.Item label={t('appMemory.enabled')} valuePropName="checked" extra={t('appMemory.enabledHint')} style={{ marginBottom: spacing.sm }}>
          <Switch
            checked={memoryEnabled}
            onChange={(checked) => {
              setMemoryEnabled(checked)
              setDirty(true)
            }}
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
                    onChange={(value) => {
                      setEmbeddingModelId(value)
                      setDirty(true)
                    }}
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
                    onChange={(value) => {
                      setEmbeddingDimensions(typeof value === 'number' ? value : null)
                      setDirty(true)
                    }}
                    placeholder="1024"
                  />
                </Form.Item>
              </Col>
            </Row>
            <Form.Item label={t('appMemory.types')} style={{ marginBottom: spacing.sm }}>
              <Space size={spacing.lg} wrap>
                <span>
                  <Switch
                    size="small"
                    checked={enableSemantic}
                    onChange={(v) => {
                      setEnableSemantic(v)
                      setDirty(true)
                    }}
                  />{' '}
                  {t('appMemory.typeSemantic')}
                </span>
                <span>
                  <Switch
                    size="small"
                    checked={enableEpisodic}
                    onChange={(v) => {
                      setEnableEpisodic(v)
                      setDirty(true)
                    }}
                  />{' '}
                  {t('appMemory.typeEpisodic')}
                </span>
                <span>
                  <Switch
                    size="small"
                    checked={enableProcedural}
                    onChange={(v) => {
                      setEnableProcedural(v)
                      setDirty(true)
                    }}
                  />{' '}
                  {t('appMemory.typeProcedural')}
                </span>
              </Space>
            </Form.Item>
            <Form.Item label={t('appMemory.autoExtract')} valuePropName="checked" extra={t('appMemory.autoExtractHint')} style={{ marginBottom: spacing.sm }}>
              <Switch
                checked={autoExtract}
                onChange={(v) => {
                  setAutoExtract(v)
                  setDirty(true)
                }}
              />
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
                    onChange={(value) => {
                      setRetrievalTopK(typeof value === 'number' ? value : null)
                      setDirty(true)
                    }}
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
                    onChange={(value) => {
                      setMaxPerUser(typeof value === 'number' ? value : null)
                      setDirty(true)
                    }}
                    placeholder="500"
                  />
                </Form.Item>
              </Col>
            </Row>
          </>
        )}
      </Form>
      {canManage && (
        <Button type="primary" style={{ marginTop: spacing.sm }} loading={saving} disabled={!dirty} onClick={() => void handleSave()}>
          {t('appManage.saveConfig')}
        </Button>
      )}

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
    </DSCard>
  )
}

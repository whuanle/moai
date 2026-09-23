import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import {
  DeleteOutlined,
  EditOutlined,
  PlayCircleOutlined,
  PlusOutlined,
  ReloadOutlined,
} from '@ant-design/icons'
import { Button, Form, Input, Modal, Popconfirm, Select, Space, Tag, Tooltip , theme } from 'antd'
import type { TableColumnsType } from 'antd'
import Editor from '@monaco-editor/react'
import { useTranslation } from 'react-i18next'
import type { PluginClassify } from '@/api/classify'
import { getKnowledgeGraphs, getKnowledgeGraphSchema } from '@/api/knowledgeGraph'
import type { DynamicPluginTemplate } from '@/api/plugin'
import {
  deleteTeamPlugin,
  getTeamDynamicTemplates,
  runTeamPlugin,
  saveTeamDynamicPlugin,
  type TeamDynamicPluginItem,
} from '@/api/team-plugin'
import { DataTable, feedback } from '@/design-system'
import { formatDateTime } from '@/utils/datetime'
import { PluginRunDrawer } from '@/pages/plugins/components/PluginRunDrawer'

interface DynamicFormValues {
  instanceKey: string
  templeteKey?: string
  title: string
  description?: string
  classifyId?: number
  kgId?: number
  config: string
}

const KG_CYPHER_TEMPLATE_KEY = 'kg_cypher_query'

/** 从实例配置 JSON 中容错解析 KgId（编辑回显用） */
function parseKgIdFromConfig(config: string | null | undefined): number | undefined {
  try {
    const kgId = Number((JSON.parse(config ?? '{}') as { KgId?: unknown }).KgId)
    return Number.isFinite(kgId) && kgId > 0 ? kgId : undefined
  } catch {
    return undefined
  }
}

/**
 * kg_cypher_query：按选中图谱预填描述与配置（模型写对 Cypher 的第一喂养位）。
 */
async function prefillKgCypherQuery(
  teamId: number,
  kgId: number,
  setFields: (description: string, config: string) => void,
): Promise<void> {
  const graphs = await getKnowledgeGraphs(teamId)
  const graph = (graphs.items ?? []).find((g) => Number(g.kgId) === kgId)
  if (!graph) return
  let summary = ''
  try {
    const schema = await getKnowledgeGraphSchema(kgId)
    const entityNames = (schema.entityTypes ?? []).map((x) => x.name ?? '').filter(Boolean)
    const relationNames = (schema.relationTypes ?? []).map((x) => x.name ?? '').filter(Boolean)
    summary = `实体类型：${entityNames.join('、') || '（未定义）'}；关系类型：${relationNames.join('、') || '（未定义）'}。`
  } catch {
    summary = ''
  }
  const usage =
    graph.mode === 'connected'
      ? '接入图谱：节点使用原生 label 与关系类型，查询无需 $kgId 过滤。'
      : '托管图谱：节点标签为 KgNode（含 name/description 属性），所有 MATCH 必须带 {kgId: $kgId} 过滤，$kgId 由系统自动注入。'
  const description = `${graph.description || graph.name || ''}。${summary}${usage}首次使用可传 {"Schema": true} 获取图谱结构。`
  setFields(description.slice(0, 255), JSON.stringify({ KgId: kgId, MaxRows: 200, TimeoutSeconds: 30 }, null, 2))
}

interface TeamDynamicPluginPanelProps {
  teamId: number
  items: TeamDynamicPluginItem[]
  loading: boolean
  canManage: boolean
  classifies: PluginClassify[]
  reload: () => void
}

export function TeamDynamicPluginPanel({
  teamId,
  items,
  loading,
  canManage,
  classifies,
  reload,
}: TeamDynamicPluginPanelProps) {
  const { t } = useTranslation()
  const { token } = theme.useToken()
  const [templates, setTemplates] = useState<DynamicPluginTemplate[]>([])
  const [modalOpen, setModalOpen] = useState(false)
  const [submitting, setSubmitting] = useState(false)
  const [editing, setEditing] = useState<TeamDynamicPluginItem | null>(null)
  const [drawerTarget, setDrawerTarget] = useState<TeamDynamicPluginItem | null>(null)
  const [filter, setFilter] = useState('all')
  const [form] = Form.useForm<DynamicFormValues>()
  const selectedTempleteKey = Form.useWatch('templeteKey', form)
  const isKgCypherTemplate = selectedTempleteKey === KG_CYPHER_TEMPLATE_KEY
  const [kgGraphOptions, setKgGraphOptions] = useState<{ value: number; label: string }[]>([])
  const [kgEnabled, setKgEnabled] = useState(true)
  const [kgGraphsLoading, setKgGraphsLoading] = useState(false)
  const kgGraphsLoadedRef = useRef(false)

  const loadTemplates = useCallback(async () => {
    try {
      const res = await getTeamDynamicTemplates(teamId)
      setTemplates((res.items ?? []).filter((i) => i.isDynamic === true))
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }, [teamId])

  useEffect(() => {
    void loadTemplates()
  }, [loadTemplates])

  const loadKgGraphOptions = useCallback(async () => {
    setKgGraphsLoading(true)
    try {
      const res = await getKnowledgeGraphs(teamId)
      setKgEnabled(res.enabled === true)
      setKgGraphOptions(
        (res.items ?? [])
          .filter((g) => g.kgId != null)
          .map((g) => ({ value: Number(g.kgId), label: g.name ?? String(g.kgId) })),
      )
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setKgGraphsLoading(false)
    }
  }, [teamId])

  // kg_cypher_query：弹窗打开且模板命中时懒加载一次本团队图谱列表
  useEffect(() => {
    if (!modalOpen || !isKgCypherTemplate || kgGraphsLoadedRef.current) return
    kgGraphsLoadedRef.current = true
    void loadKgGraphOptions()
  }, [modalOpen, isKgCypherTemplate, loadKgGraphOptions])

  const reset = () => {
    setEditing(null)
    form.resetFields()
    kgGraphsLoadedRef.current = false
  }

  const openCreate = () => {
    reset()
    setModalOpen(true)
  }

  const openEdit = (record: TeamDynamicPluginItem) => {
    kgGraphsLoadedRef.current = false
    setEditing(record)
    form.setFieldsValue({
      instanceKey: record.instanceKey ?? record.pluginName ?? '',
      templeteKey: record.templeteKey ?? undefined,
      title: record.title ?? '',
      description: record.description ?? '',
      classifyId: record.classifyId || undefined,
      kgId: parseKgIdFromConfig(record.config),
      config: record.config ?? '{}',
    })
    setModalOpen(true)
  }

  // kg_cypher_query：选中图谱后自动预填描述与配置，仅作起点，用户可手改。
  // 响应落地前复核绑定未变（含已切换/已清空/已切模板/弹窗已重置），丢弃过期响应，防绑定与展示脱节。
  const handleKgBindingChange = async (graphId: number) => {
    const isStale = () =>
      form.getFieldValue('kgId') !== graphId || form.getFieldValue('templeteKey') !== KG_CYPHER_TEMPLATE_KEY
    try {
      await prefillKgCypherQuery(teamId, graphId, (description, config) => {
        if (isStale()) return
        form.setFieldsValue({ description, config })
      })
    } catch {
      if (isStale()) return
      feedback.error(t('plugins.kgBindingLoadFailed'))
    }
  }

  const isDuplicateKey = (instanceKey: string) => items.some((i) => (i.instanceKey ?? i.pluginName) === instanceKey)

  const handleSubmit = async () => {
    const values = await form.validateFields()
    const instanceKey = (values.instanceKey ?? '').trim()
    const templeteKey = editing ? editing.templeteKey : values.templeteKey
    if (!instanceKey || !templeteKey) return

    if (!editing && isDuplicateKey(instanceKey)) {
      feedback.error(t('plugins.dynamicKeyExists'))
      return
    }

    setSubmitting(true)
    try {
      await saveTeamDynamicPlugin({
        teamId,
        instanceKey,
        templeteKey,
        title: values.title,
        description: values.description ?? '',
        config: values.config ?? '{}',
        classifyId: values.classifyId ?? 0,
      })
      feedback.success(t('plugins.updateSuccess'))
      reset()
      setModalOpen(false)
      reload()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setSubmitting(false)
    }
  }

  const handleDelete = async (record: TeamDynamicPluginItem) => {
    if (!record.pluginId) return
    await deleteTeamPlugin(teamId, String(record.pluginId))
    feedback.success(t('plugins.deletePluginSuccess'))
    if (editing?.pluginId === record.pluginId) reset()
    reload()
  }

  const filtered = useMemo(() => {
    if (filter === 'all') return items
    if (filter === 'uncategorized') return items.filter((i) => (i.classifyId ?? 0) === 0)
    const id = Number(filter)
    return items.filter((i) => i.classifyId === id)
  }, [items, filter])

  const filterTags = useMemo(
    () => [
      { value: 'all', label: t('plugins.classifyAll') },
      { value: 'uncategorized', label: t('plugins.classifyUncategorized') },
      ...classifies.map((c) => ({ value: String(c.classifyId), label: c.name })),
    ],
    [classifies, t],
  )

  const columns: TableColumnsType<TeamDynamicPluginItem> = [
    { title: t('plugins.colPluginName'), dataIndex: 'pluginName', width: 180 },
    { title: t('plugins.colTitle'), dataIndex: 'title', width: 150, ellipsis: true },
    {
      title: t('plugins.dynamicTemplate'),
      dataIndex: 'templeteKey',
      width: 160,
      render: (v: string | null) => (v ? <Tag color="blue">{v}</Tag> : '-'),
    },
    {
      title: t('plugins.colIsSystem'),
      dataIndex: 'isTeamOwned',
      width: 100,
      render: (v: boolean | null) =>
        v ? <Tag color="green">{t('plugins.teamOwned')}</Tag> : <Tag color="geekblue">{t('plugins.isSystem')}</Tag>,
    },
    {
      title: t('plugins.colClassify'),
      dataIndex: 'classifyName',
      width: 130,
      render: (v: string | null) =>
        v ? <Tag>{v}</Tag> : <Tag color="orange">{t('plugins.classifyUncategorized')}</Tag>,
    },
    { title: t('plugins.colCreateUser'), dataIndex: 'createUserName', width: 110, ellipsis: true, render: (v: string | null) => v || '-' },
    { title: t('plugins.colCreateTime'), dataIndex: 'createTime', width: 160, render: (v: string | null) => (
      <span style={{ whiteSpace: 'nowrap' }}>{formatDateTime(v)}</span>
    ) },
    { title: t('plugins.colUpdateUser'), dataIndex: 'updateUserName', width: 110, ellipsis: true, render: (v: string | null) => v || '-' },
    { title: t('plugins.colUpdateTime'), dataIndex: 'updateTime', width: 160, render: (v: string | null) => (
      <span style={{ whiteSpace: 'nowrap' }}>{formatDateTime(v)}</span>
    ) },
    {
      title: t('plugins.colActions'),
      key: 'actions',
      width: 150,
      fixed: 'right',
      render: (_, record) => (
        <Space size={0}>
          <Tooltip title={t('plugins.run')}>
            <Button
              type="text"
              size="small"
              icon={<PlayCircleOutlined />}
              aria-label={t('plugins.run')}
              onClick={() => setDrawerTarget(record)}
            />
          </Tooltip>
          {canManage && record.isTeamOwned && (
            <>
              <Tooltip title={t('plugins.editPlugin')}>
                <Button
                  type="text"
                  size="small"
                  icon={<EditOutlined />}
                  aria-label={t('plugins.editPlugin')}
                  onClick={() => openEdit(record)}
                />
              </Tooltip>
              <Popconfirm
                title={t('plugins.deletePluginConfirm')}
                okButtonProps={{ danger: true }}
                onConfirm={() => void handleDelete(record)}
              >
                <Tooltip title={t('plugins.deletePlugin')}>
                  <Button type="text" size="small" danger icon={<DeleteOutlined />} aria-label={t('plugins.deletePlugin')} />
                </Tooltip>
              </Popconfirm>
            </>
          )}
        </Space>
      ),
    },
  ]

  return (
    <>
      <DataTable<TeamDynamicPluginItem>
        sticky
        rowKey={(r) => String(r.pluginId ?? r.pluginName ?? '')}
        columns={columns}
        dataSource={filtered}
        loading={loading}
        pagination={false}
        scroll={{ x: 1300 }}
        toolbar={
          <div style={{ display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: 8 }}>
            {canManage && (
              <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
                {t('plugins.createDynamicInstance')}
              </Button>
            )}
            <Button icon={<ReloadOutlined />} onClick={reload} loading={loading}>
              {t('plugins.refresh')}
            </Button>
            {filterTags.map((item, index) => (
              <span key={item.value} style={{ display: 'inline-flex', alignItems: 'center', gap: 8 }}>
                {index > 0 && (
                  <span className="classify-divider" style={{ color: token.colorTextQuaternary }}>
                    |
                  </span>
                )}
                <Tag.CheckableTag checked={filter === item.value} onChange={() => setFilter(item.value)}>
                  {item.label}
                </Tag.CheckableTag>
              </span>
            ))}
          </div>
        }
      />

      {drawerTarget && (
        <PluginRunDrawer
          open={Boolean(drawerTarget)}
          onClose={() => setDrawerTarget(null)}
          pluginKey={drawerTarget.pluginName ?? drawerTarget.instanceKey}
          paramsExample={drawerTarget.paramsExample}
          runPlugin={(payload) => runTeamPlugin({ teamId, ...payload })}
        />
      )}

      <Modal
        open={modalOpen}
        title={editing ? t('plugins.editDynamicInstance') : t('plugins.createDynamicInstance')}
        onCancel={() => {
          reset()
          setModalOpen(false)
        }}
        onOk={handleSubmit}
        okText={t('plugins.save')}
        confirmLoading={submitting}
        maskClosable={false}
        destroyOnClose
        width={640}
      >
        <Form form={form} layout="vertical">
          <Form.Item
            name="instanceKey"
            label={t('plugins.pluginKey')}
            rules={[
              { required: true, message: t('plugins.pluginKeyRequired') },
              { pattern: /^[a-z_][a-z0-9_]*$/, message: t('plugins.pluginKeyRule') },
              { max: 30, message: t('plugins.pluginKeyMax') },
            ]}
          >
            <Input disabled={Boolean(editing)} maxLength={30} placeholder={t('plugins.pluginKeyPlaceholder')} />
          </Form.Item>
          <Form.Item
            name="templeteKey"
            label={t('plugins.dynamicTemplate')}
            rules={[{ required: true, message: t('plugins.dynamicTemplateRequired') }]}
          >
            <Select
              disabled={Boolean(editing)}
              allowClear
              showSearch
              optionFilterProp="label"
              placeholder={t('plugins.dynamicTemplatePlaceholder')}
              options={templates.map((tp) => ({ value: tp.key, label: `${tp.name} (${tp.key})` }))}
              onChange={(v) => {
                const tp = templates.find((x) => x.key === v)
                if (tp) form.setFieldValue('config', tp.configExample ?? '{}')
              }}
            />
          </Form.Item>
          {isKgCypherTemplate && (
            <Form.Item name="kgId" label={t('plugins.kgBinding')}>
              <Select
                allowClear
                showSearch
                optionFilterProp="label"
                loading={kgGraphsLoading}
                disabled={!kgEnabled}
                placeholder={kgEnabled ? t('plugins.kgBindingPlaceholder') : t('plugins.kgBindingDisabled')}
                options={kgGraphOptions}
                onChange={(v) => {
                  if (typeof v === 'number') void handleKgBindingChange(v)
                }}
              />
            </Form.Item>
          )}
          <Form.Item name="title" label={t('plugins.formPluginTitle')} rules={[{ required: true, message: t('plugins.pluginTitleRequired') }]}>
            <Input maxLength={30} />
          </Form.Item>
          <Form.Item name="description" label={t('plugins.formDescription')}>
            <Input.TextArea maxLength={255} />
          </Form.Item>
          <Form.Item name="classifyId" label={t('plugins.formClassify')}>
            <Select
              allowClear
              placeholder={t('plugins.formClassifyPlaceholder')}
              options={classifies.map((c) => ({ value: c.classifyId, label: c.name }))}
            />
          </Form.Item>
          <Form.Item name="config" label={t('plugins.config')} rules={[{ required: true, message: t('plugins.configRequired') }]}>
            <Editor
              height="200px"
              language="json"
              value={form.getFieldValue('config') ?? '{}'}
              onChange={(v) => form.setFieldValue('config', v ?? '{}')}
              options={{ minimap: { enabled: false }, fontSize: 14 }}
            />
          </Form.Item>
        </Form>
      </Modal>
    </>
  )
}

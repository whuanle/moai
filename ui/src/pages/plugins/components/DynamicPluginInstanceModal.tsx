import { useEffect, useRef, useState } from 'react'
import { Form, Input, Modal, Select, Typography } from 'antd'
import Editor from '@monaco-editor/react'
import { useTranslation } from 'react-i18next'
import { classifyLabel, type PluginClassify } from '@/api/classify'
import { getKnowledgeGraphs, getKnowledgeGraphSchema } from '@/api/knowledgeGraph'
import { pluginApi, type DynamicPluginTemplate } from '@/api/plugin'
import { saveTeamDynamicPlugin } from '@/api/team-plugin'
import { feedback } from '@/design-system'
import { PluginAvatarUpload } from './PluginAvatarUpload'

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

/** 创建/编辑动态插件实例所需的最小字段集（系统侧管理项与团队侧列表项均满足）. */
export interface DynamicPluginInstanceEditable {
  id?: string | null
  pluginName?: string | null
  instanceKey?: string | null
  templeteKey?: string | null
  title?: string | null
  description?: string | null
  classifyId?: number | null
  config?: string | null
  avatarPath?: string | null
}

interface DynamicPluginInstanceModalProps {
  open: boolean
  /** system=管理员系统实例（/api/ai/plugin/dynamic/save）；team=团队实例（/api/team/{teamId}/plugin/dynamic）. */
  scope: 'system' | 'team'
  /** scope=team 时必填. */
  teamId?: number
  templates: DynamicPluginTemplate[]
  classifies: PluginClassify[]
  /** 已占用实例 key（创建时前端查重；团队侧应包含系统插件 key 以对齐全局保留语义）. */
  existingKeys: string[]
  /** 传入即编辑模式（实例 key 与模板不可改）. */
  editing?: DynamicPluginInstanceEditable | null
  /** 新建时预选模板并预填配置示例（模板列表卡片「新建」入口）. */
  presetTemplateKey?: string | null
  onSaved: () => void
  onClose: () => void
}

/** 从实例配置 JSON 中容错解析 KgId（编辑回显用）. */
function parseKgIdFromConfig(config: string | null | undefined): number | undefined {
  try {
    const kgId = Number((JSON.parse(config ?? '{}') as { KgId?: unknown }).KgId)
    return Number.isFinite(kgId) && kgId > 0 ? kgId : undefined
  } catch {
    return undefined
  }
}

/**
 * kg_cypher_query：按选中图谱预填描述与配置（模型写对 Cypher 的第一喂养位）.
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

/** 动态插件实例创建/编辑模态：系统侧与团队侧共用（字段差异由 scope 决定）. */
export function DynamicPluginInstanceModal({
  open,
  scope,
  teamId,
  templates,
  classifies,
  existingKeys,
  editing = null,
  presetTemplateKey = null,
  onSaved,
  onClose,
}: DynamicPluginInstanceModalProps) {
  const { t } = useTranslation()
  const [submitting, setSubmitting] = useState(false)
  const [form] = Form.useForm<DynamicFormValues>()
  const selectedTempleteKey = Form.useWatch('templeteKey', form)
  const isKgCypherTemplate = selectedTempleteKey === KG_CYPHER_TEMPLATE_KEY
  const [kgGraphOptions, setKgGraphOptions] = useState<{ value: number; label: string }[]>([])
  const [kgEnabled, setKgEnabled] = useState(true)
  const [kgGraphsLoading, setKgGraphsLoading] = useState(false)
  const kgGraphsLoadedRef = useRef(false)

  useEffect(() => {
    if (!open) return
    kgGraphsLoadedRef.current = false
    if (editing) {
      form.setFieldsValue({
        instanceKey: editing.instanceKey ?? editing.pluginName ?? '',
        templeteKey: editing.templeteKey ?? undefined,
        title: editing.title ?? '',
        description: editing.description ?? '',
        classifyId: editing.classifyId || undefined,
        kgId: parseKgIdFromConfig(editing.config),
        config: editing.config ?? '{}',
      })
    } else if (presetTemplateKey) {
      const tp = templates.find((x) => x.key === presetTemplateKey)
      form.resetFields()
      form.setFieldsValue({ templeteKey: presetTemplateKey, config: tp?.configExample ?? '{}' })
    } else {
      form.resetFields()
      form.setFieldValue('config', '{}')
    }
  }, [open, editing, presetTemplateKey, templates, form])

  const loadKgGraphOptions = async () => {
    if (scope !== 'team' || !teamId) return
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
  }

  // kg_cypher_query：弹窗打开且模板命中时懒加载一次本团队图谱列表
  useEffect(() => {
    if (!open || !isKgCypherTemplate || kgGraphsLoadedRef.current) return
    kgGraphsLoadedRef.current = true
    void loadKgGraphOptions()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, isKgCypherTemplate, scope, teamId])

  // kg_cypher_query：选中图谱后自动预填描述与配置，仅作起点，用户可手改。
  // 响应落地前复核绑定未变（含已切换/已清空/已切模板/弹窗已重置），丢弃过期响应，防绑定与展示脱节。
  const handleKgBindingChange = async (graphId: number) => {
    if (!teamId) return
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

  const handleSubmit = async () => {
    let values: DynamicFormValues
    try {
      values = await form.validateFields()
    } catch {
      return
    }
    const instanceKey = (values.instanceKey ?? '').trim()
    const templeteKey = editing ? editing.templeteKey : values.templeteKey
    if (!instanceKey || !templeteKey) return

    if (!editing && existingKeys.includes(instanceKey)) {
      feedback.error(t('plugins.dynamicKeyExists'))
      return
    }

    setSubmitting(true)
    try {
      if (scope === 'team') {
        await saveTeamDynamicPlugin({
          teamId: teamId ?? 0,
          instanceKey,
          templeteKey,
          title: values.title,
          description: values.description ?? '',
          config: values.config ?? '{}',
          classifyId: values.classifyId ?? 0,
        })
      } else {
        await pluginApi.saveDynamicPlugin({
          pluginKey: instanceKey,
          templeteKey,
          title: values.title,
          description: values.description ?? '',
          classifyId: values.classifyId ?? 0,
          config: values.config ?? '{}',
        })
      }
      feedback.success(t('plugins.updateSuccess'))
      onSaved()
      onClose()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <Modal
      open={open}
      title={editing ? t('plugins.editDynamicInstance') : t('plugins.createDynamicInstance')}
      onCancel={onClose}
      onOk={handleSubmit}
      okText={t('plugins.save')}
      confirmLoading={submitting}
      maskClosable={false}
      destroyOnClose
      width={640}
    >
      <Form form={form} layout="vertical">
        {scope === 'system' && editing && (
          <div style={{ display: 'flex', alignItems: 'center', gap: 12, marginBottom: 16 }}>
            <PluginAvatarUpload
              pluginId={editing.id ?? ''}
              objectKey={editing.avatarPath}
              title={editing.title}
              onChanged={onSaved}
            />
            <Typography.Text type="secondary">{t('plugins.avatarTip')}</Typography.Text>
          </div>
        )}
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
        {scope === 'team' && isKgCypherTemplate && (
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
            options={classifies.map((c) => ({ value: c.classifyId, label: classifyLabel(c) }))}
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
  )
}

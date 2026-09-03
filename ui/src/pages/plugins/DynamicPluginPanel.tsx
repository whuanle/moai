import { useCallback, useEffect, useState } from 'react'
import { PlusOutlined, ReloadOutlined } from '@ant-design/icons'
import { Button, Form, Input, Modal, Popconfirm, Select, Space, Tag } from 'antd'
import type { TableColumnsType } from 'antd'
import Editor from '@monaco-editor/react'
import { useTranslation } from 'react-i18next'
import type { PluginClassify } from '@/api/classify'
import {
  pluginApi,
  type DynamicPluginManageItem,
  type DynamicPluginTemplate,
} from '@/api/plugin'
import { DataTable, feedback } from '@/design-system'
import { PluginRunDrawer } from './components/PluginRunDrawer'

function formatDateTime(value: string | null | undefined): string {
  if (!value) return '-'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return '-'
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())} ${pad(date.getHours())}:${pad(date.getMinutes())}`
}

interface DynamicPluginPanelProps {
  classifies: PluginClassify[]
}

interface DynamicFormValues {
  pluginKey: string
  templeteKey?: string
  title: string
  description?: string
  classifyId?: number
  config: string
}

export function DynamicPluginPanel({ classifies }: DynamicPluginPanelProps) {
  const { t } = useTranslation()
  const [items, setItems] = useState<DynamicPluginManageItem[]>([])
  const [templates, setTemplates] = useState<DynamicPluginTemplate[]>([])
  const [loading, setLoading] = useState(true)
  const [modalOpen, setModalOpen] = useState(false)
  const [submitting, setSubmitting] = useState(false)
  const [editing, setEditing] = useState<DynamicPluginManageItem | null>(null)
  const [drawerTarget, setDrawerTarget] = useState<DynamicPluginManageItem | null>(null)
  const [form] = Form.useForm<DynamicFormValues>()

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const [instances, tmpl] = await Promise.all([
        pluginApi.getManagePlugins('dynamic'),
        pluginApi.getDynamicTemplates(),
      ])
      setItems(instances as DynamicPluginManageItem[])
      setTemplates(tmpl)
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    void load()
  }, [load])

  const reset = () => {
    setEditing(null)
    form.resetFields()
  }

  const openCreate = () => {
    reset()
    setModalOpen(true)
  }

  const openEdit = (record: DynamicPluginManageItem) => {
    setEditing(record)
    form.setFieldsValue({
      pluginKey: record.pluginName ?? '',
      templeteKey: record.templeteKey ?? undefined,
      title: record.title ?? '',
      description: record.description ?? '',
      classifyId: record.classifyId || undefined,
      config: record.config ?? '{}',
    })
    setModalOpen(true)
  }

  const isDuplicateKey = (pluginKey: string) => items.some((i) => i.pluginName === pluginKey)

  const handleSubmit = async () => {
    const values = await form.validateFields()
    const pluginKey = (values.pluginKey ?? '').trim()
    const templeteKey = editing ? editing.templeteKey : values.templeteKey
    if (!pluginKey || !templeteKey) return

    if (!editing && isDuplicateKey(pluginKey)) {
      feedback.error(t('plugins.dynamicKeyExists'))
      return
    }

    setSubmitting(true)
    try {
      await pluginApi.saveDynamicPlugin({
        pluginKey,
        templeteKey,
        title: values.title,
        description: values.description ?? '',
        classifyId: values.classifyId ?? 0,
        config: values.config ?? '{}',
      })
      feedback.success(t('plugins.updateSuccess'))
      reset()
      setModalOpen(false)
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setSubmitting(false)
    }
  }

  const handleDelete = async (record: DynamicPluginManageItem) => {
    const pluginKey = record.pluginName ?? record.pluginKey
    if (!pluginKey) return
    try {
      await pluginApi.deleteDynamicPlugin(pluginKey)
      feedback.success(t('plugins.deletePluginSuccess'))
      if (editing?.pluginName === pluginKey) reset()
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const columns: TableColumnsType<DynamicPluginManageItem> = [
    { title: t('plugins.colPluginName'), dataIndex: 'pluginName', width: 180 },
    { title: t('plugins.colTitle'), dataIndex: 'title', width: 150, ellipsis: true },
    {
      title: t('plugins.dynamicTemplate'),
      dataIndex: 'templeteKey',
      width: 160,
      render: (v: string | null) => (v ? <Tag color="blue">{v}</Tag> : '-'),
    },
    {
      title: t('plugins.colClassify'),
      dataIndex: 'classifyName',
      width: 130,
      render: (v: string | null) =>
        v ? <Tag>{v}</Tag> : <Tag color="orange">{t('plugins.classifyUncategorized')}</Tag>,
    },
    { title: t('plugins.colCreateTime'), dataIndex: 'createTime', width: 160, render: (v: string | null) => formatDateTime(v) },
    {
      title: t('plugins.colActions'),
      key: 'actions',
      width: 150,
      fixed: 'right',
      render: (_: unknown, record: DynamicPluginManageItem) => (
        <Space>
          <Button type="text" size="small" onClick={() => setDrawerTarget(record)}>
            {t('plugins.run')}
          </Button>
          <Button type="text" size="small" onClick={() => openEdit(record)}>
            {t('plugins.editPlugin')}
          </Button>
          <Popconfirm
            title={t('plugins.deletePluginConfirm')}
            okButtonProps={{ danger: true }}
            onConfirm={() => void handleDelete(record)}
          >
            <Button type="text" size="small" danger aria-label={t('plugins.deletePlugin')}>
              {t('plugins.deletePlugin')}
            </Button>
          </Popconfirm>
        </Space>
      ),
    },
  ]

  return (
    <>
      <DataTable<DynamicPluginManageItem>
        rowKey="id"
        columns={columns}
        dataSource={items}
        loading={loading}
        pagination={false}
        toolbar={
          <Space>
            <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
              {t('plugins.createDynamicInstance')}
            </Button>
            <Button icon={<ReloadOutlined />} onClick={load} loading={loading}>
              {t('plugins.refresh')}
            </Button>
          </Space>
        }
      />

      {drawerTarget && (
        <PluginRunDrawer
          open={Boolean(drawerTarget)}
          onClose={() => setDrawerTarget(null)}
          pluginKey={drawerTarget.pluginName ?? drawerTarget.pluginKey}
          paramsExample={drawerTarget.paramsExample}
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
            name="pluginKey"
            label={t('plugins.pluginKey')}
            rules={[
              { required: true, message: t('plugins.pluginKeyRequired') },
              {
                pattern: /^[a-z_][a-z0-9_]*$/,
                message: t('plugins.pluginKeyRule'),
              },
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
              placeholder={t('plugins.dynamicTemplatePlaceholder')}
              options={templates.map((tp) => ({ value: tp.key, label: `${tp.name} (${tp.key})` }))}
              onChange={(v) => {
                const tp = templates.find((x) => x.key === v)
                if (tp) form.setFieldValue('config', tp.configExample ?? '{}')
              }}
            />
          </Form.Item>
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

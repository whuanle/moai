import { useCallback, useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button, Card, Col, Form, Input, Modal, Popconfirm, Row, Select, Space } from 'antd'
import { DeleteOutlined, EditOutlined, PlusOutlined } from '@ant-design/icons'
import { feedback } from '@/design-system'
import { spacing } from '@/design-system/theme'
import {
  createKnowledgeGraph,
  deleteKnowledgeGraph,
  getKnowledgeGraphTemplates,
  getKnowledgeGraphs,
  updateKnowledgeGraph,
  type KnowledgeGraphItem,
  type KnowledgeGraphTemplateItem,
} from '@/api/knowledgeGraph'

const ROLE_MEMBER = 0

interface FormValues {
  name: string
  description?: string
  templateKey?: string
}

export function TeamKnowledgeGraphs({ teamId }: { teamId: number }) {
  const { t } = useTranslation()
  const [items, setItems] = useState<KnowledgeGraphItem[]>([])
  const [myRole, setMyRole] = useState<number | null>(null)
  const [enabled, setEnabled] = useState(true)
  const [templates, setTemplates] = useState<KnowledgeGraphTemplateItem[]>([])
  const [open, setOpen] = useState(false)
  const [editing, setEditing] = useState<KnowledgeGraphItem | null>(null)
  const [saving, setSaving] = useState(false)
  const [form] = Form.useForm<FormValues>()
  const isAdminPlus = myRole !== null && myRole !== ROLE_MEMBER

  const load = useCallback(async () => {
    try {
      const res = await getKnowledgeGraphs(teamId)
      setItems(res.items ?? [])
      setMyRole(res.myRole ?? null)
      setEnabled(res.enabled ?? false)
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }, [teamId])

  useEffect(() => { void load() }, [load])

  useEffect(() => {
    if (enabled) void getKnowledgeGraphTemplates().then(setTemplates)
  }, [enabled])

  const openCreate = () => {
    setEditing(null)
    form.resetFields()
    setOpen(true)
  }

  const openEdit = (record: KnowledgeGraphItem) => {
    setEditing(record)
    form.setFieldsValue({ name: record.name ?? '', description: record.description ?? undefined })
    setOpen(true)
  }

  const handleSubmit = async () => {
    const values = await form.validateFields()
    setSaving(true)
    try {
      if (editing) {
        await updateKnowledgeGraph(Number(editing.kgId), { name: values.name, description: values.description })
        feedback.success(t('knowledgegraph.saveSuccess'))
      } else {
        await createKnowledgeGraph({ teamId, name: values.name, description: values.description, templateKey: values.templateKey })
        feedback.success(t('knowledgegraph.createSuccess'))
      }
      setOpen(false)
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setSaving(false)
    }
  }

  const handleDelete = async (record: KnowledgeGraphItem) => {
    try {
      await deleteKnowledgeGraph(Number(record.kgId))
      feedback.success(t('knowledgegraph.deleteSuccess'))
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  return (
    <>
      <Space style={{ marginBottom: spacing.md }}>
        {isAdminPlus && (
          <Button type="primary" icon={<PlusOutlined />} disabled={!enabled} onClick={openCreate}>
            {t('knowledgegraph.create')}
          </Button>
        )}
      </Space>
      {!enabled && <div style={{ marginBottom: spacing.md }}>{t('knowledgegraph.disabled')}</div>}
      <Row gutter={[spacing.md, spacing.md]}>
        {items.map((item) => (
          <Col key={String(item.kgId)} xs={24} sm={12} md={8} lg={6}>
            <Card>
              <div style={{ fontWeight: 600 }}>{item.name}</div>
              <div style={{ opacity: 0.65, minHeight: 22 }}>{item.description || '-'}</div>
              {isAdminPlus && (
                <Space>
                  <Button
                    type="text"
                    size="small"
                    icon={<EditOutlined />}
                    aria-label={t('knowledgegraph.edit')}
                    onClick={() => openEdit(item)}
                  />
                  <Popconfirm title={t('knowledgegraph.deleteConfirm')} onConfirm={() => void handleDelete(item)}>
                    <Button type="text" size="small" danger icon={<DeleteOutlined />} aria-label={t('knowledgegraph.delete')} />
                  </Popconfirm>
                </Space>
              )}
            </Card>
          </Col>
        ))}
      </Row>

      <Modal
        open={open}
        title={editing ? t('knowledgegraph.editTitle') : t('knowledgegraph.createTitle')}
        onOk={() => void handleSubmit()}
        onCancel={() => setOpen(false)}
        confirmLoading={saving}
        destroyOnHidden
        maskClosable={false}
      >
        <Form form={form} layout="vertical">
          <Form.Item
            name="name"
            label={t('knowledgegraph.name')}
            rules={[{ required: true, message: t('knowledgegraph.namePlaceholder') }]}
          >
            <Input maxLength={50} placeholder={t('knowledgegraph.namePlaceholder')} />
          </Form.Item>
          <Form.Item name="description" label={t('knowledgegraph.desc')}>
            <Input.TextArea maxLength={255} rows={2} />
          </Form.Item>
          {!editing && (
            <Form.Item name="templateKey" label={t('knowledgegraph.template')} initialValue="blank">
              <Select
                options={templates.map((x) => ({
                  value: x.key ?? '',
                  label: `${x.name}${x.entityTypes?.length ? ` (${x.entityTypes.join(' / ')})` : ''}`,
                }))}
              />
            </Form.Item>
          )}
        </Form>
      </Modal>
    </>
  )
}

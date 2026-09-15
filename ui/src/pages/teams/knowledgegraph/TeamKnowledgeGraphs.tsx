import { useCallback, useEffect, useState } from 'react'
import { useNavigate } from 'react-router'
import { useTranslation } from 'react-i18next'
import { Avatar, Button, Col, Form, Input, Modal, Popconfirm, Radio, Row, Select, Space, Tag } from 'antd'
import { ClusterOutlined, DeleteOutlined, EditOutlined, PlusOutlined } from '@ant-design/icons'
import { Card, feedback } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { resolveStorageUrl } from '@/utils/storage'
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
  mode?: 'managed' | 'connected'
  templateKey?: string
  database?: string
}

export function TeamKnowledgeGraphs({ teamId }: { teamId: number }) {
  const { t } = useTranslation()
  const navigate = useNavigate()
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
    if (enabled) void getKnowledgeGraphTemplates().then(setTemplates).catch(() => setTemplates([]))
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
    let values: FormValues
    try {
      values = await form.validateFields()
    } catch {
      // 校验失败，antd 已在表单项下展示错误
      return
    }
    setSaving(true)
    try {
      if (editing) {
        await updateKnowledgeGraph(Number(editing.kgId), { name: values.name, description: values.description })
        feedback.success(t('knowledgegraph.saveSuccess'))
      } else {
        const mode = values.mode ?? 'managed'
        await createKnowledgeGraph({
          teamId,
          name: values.name,
          description: values.description,
          mode,
          templateKey: mode === 'managed' ? values.templateKey : undefined,
          database: mode === 'connected' ? values.database : undefined,
        })
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
            <Card
              hoverable
              style={{ cursor: 'pointer' }}
              onClick={() => navigate(`/team/${teamId}/kg/${item.kgId}/entities`)}
            >
              <Space size={spacing.sm} align="start">
                <Avatar shape="square" size={40} icon={<ClusterOutlined />} src={item.avatarPath?.trim() ? resolveStorageUrl(item.avatarPath) : undefined} />
                <div style={{ fontWeight: 600 }}>{item.name}</div>
              </Space>
              {item.mode === 'connected' && (
                <div style={{ marginTop: spacing.xs }}>
                  <Tag color="blue">{t('knowledgegraph.connectedBadge')}</Tag>
                </div>
              )}
              <div style={{ opacity: 0.65, minHeight: 22 }}>{item.description || '-'}</div>
              {isAdminPlus && (
                <Space size={0} onClick={(e) => e.stopPropagation()}>
                  <Button
                    type="text"
                    size="small"
                    icon={<EditOutlined />}
                    aria-label={t('knowledgegraph.edit')}
                    onClick={() => openEdit(item)}
                  />
                  <Popconfirm
                    title={item.mode === 'connected' ? t('knowledgegraph.deleteConnectedConfirm') : t('knowledgegraph.deleteConfirm')}
                    onConfirm={() => void handleDelete(item)}
                  >
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
            <>
              <Form.Item name="mode" label={t('knowledgegraph.mode')} initialValue="managed">
                <Radio.Group>
                  <Radio value="managed">{t('knowledgegraph.modeManaged')}</Radio>
                  <Radio value="connected">{t('knowledgegraph.modeConnected')}</Radio>
                </Radio.Group>
              </Form.Item>
              <Form.Item noStyle shouldUpdate={(p, c) => p.mode !== c.mode}>
                {({ getFieldValue }) => getFieldValue('mode') === 'connected' ? (
                  <Form.Item
                    name="database"
                    label={t('knowledgegraph.database')}
                    rules={[{ required: true, message: t('knowledgegraph.databasePlaceholder') }]}
                  >
                    <Input maxLength={100} placeholder={t('knowledgegraph.databasePlaceholder')} />
                  </Form.Item>
                ) : (
                  <Form.Item name="templateKey" label={t('knowledgegraph.template')} initialValue="blank">
                    <Select
                      options={templates.map((x) => ({
                        value: x.key ?? '',
                        label: `${x.name}${x.entityTypes?.length ? ` (${x.entityTypes.join(' / ')})` : ''}`,
                      }))}
                    />
                  </Form.Item>
                )}
              </Form.Item>
            </>
          )}
        </Form>
      </Modal>
    </>
  )
}

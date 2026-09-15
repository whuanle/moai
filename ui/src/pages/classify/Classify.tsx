import { useCallback, useEffect, useState } from 'react'
import { Button, Col, Empty, Form, Input, Modal, Popconfirm, Row, Space, Spin, Tabs, Typography } from 'antd'
import { DeleteOutlined, EditOutlined, PlusOutlined, ReloadOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import { Navigate } from 'react-router'
import { classifyApi, ClassifyType, type Classify, type ClassifyTypeKey } from '@/api/classify'
import { Card, feedback, Page } from '@/design-system'
import { fontSize, spacing } from '@/design-system/theme'
import { useAppStore } from '@/store/app'

const { Text, Paragraph } = Typography

const TYPE_TABS: { key: ClassifyTypeKey; labelKey: string }[] = [
  { key: ClassifyType.Plugin, labelKey: 'classify.typePlugin' },
  { key: ClassifyType.App, labelKey: 'classify.typeApp' },
  { key: ClassifyType.Kb, labelKey: 'classify.typeKb' },
  { key: ClassifyType.Prompt, labelKey: 'classify.typePrompt' },
]

type ClassifyForm = { name: string; description?: string }

/** 统一展示为 YYYY-MM-DD HH:mm，避免各浏览器 locale 差异. */
function formatDateTime(value: string | null | undefined): string {
  if (!value) return '-'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return '-'
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())} ${pad(date.getHours())}:${pad(date.getMinutes())}`
}

function MetaRow({ label, value }: { label: string; value: string }) {
  return (
    <div style={{ display: 'flex', gap: spacing.sm }}>
      <Text type="secondary" style={{ fontSize: fontSize.sm, flexShrink: 0 }}>
        {label}
      </Text>
      <Text type="secondary" style={{ fontSize: fontSize.sm }}>
        {value}
      </Text>
    </div>
  )
}

interface ClassifyCardItemProps {
  item: Classify
  onEdit: (item: Classify) => void
  onDelete: (item: Classify) => void
}

function ClassifyCardItem({ item, onEdit, onDelete }: ClassifyCardItemProps) {
  const { t } = useTranslation()
  return (
    <Card styles={{ body: { padding: 20, display: 'flex', flexDirection: 'column', height: '100%' } }}>
      <Text strong style={{ fontSize: 16, display: 'block' }}>
        {item.name || '-'}
      </Text>
      <Paragraph type="secondary" style={{ marginTop: spacing.xs, marginBottom: spacing.sm }}>
        {item.description || '-'}
      </Paragraph>
      <div style={{ display: 'flex', flexDirection: 'column', gap: spacing.xxs, marginTop: 'auto' }}>
        <MetaRow label={t('classify.colCreateUser')} value={`${item.createUserName || '-'} · ${formatDateTime(item.createTime)}`} />
        <MetaRow label={t('classify.colUpdateUser')} value={`${item.updateUserName || '-'} · ${formatDateTime(item.updateTime)}`} />
      </div>
      <div style={{ marginTop: spacing.sm, display: 'flex', justifyContent: 'flex-end', borderTop: '1px solid rgba(16, 24, 40, 0.08)', paddingTop: spacing.sm }}>
        <Space>
          <Button type="text" size="small" icon={<EditOutlined />} onClick={() => onEdit(item)}>
            {t('classify.edit')}
          </Button>
          <Popconfirm
            title={t('classify.deleteConfirm')}
            okButtonProps={{ danger: true }}
            onConfirm={() => onDelete(item)}
          >
            <Button type="text" size="small" danger icon={<DeleteOutlined />}>
              {t('classify.delete')}
            </Button>
          </Popconfirm>
        </Space>
      </div>
    </Card>
  )
}

function ClassifyPanel({ type }: { type: ClassifyTypeKey }) {
  const { t } = useTranslation()
  const [items, setItems] = useState<Classify[]>([])
  const [loading, setLoading] = useState(true)
  const [modalOpen, setModalOpen] = useState(false)
  const [editing, setEditing] = useState<Classify | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const [form] = Form.useForm<ClassifyForm>()

  const load = useCallback(async () => {
    setLoading(true)
    try {
      setItems(await classifyApi.getClassifies(type))
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoading(false)
    }
  }, [type])

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

  const openEdit = (record: Classify) => {
    setEditing(record)
    form.setFieldsValue({ name: record.name ?? '', description: record.description ?? '' })
    setModalOpen(true)
  }

  const handleOk = async () => {
    const values = await form.validateFields()
    const name = values.name.trim()
    if (!name) {
      feedback.error(t('classify.nameRequired'))
      return
    }
    setSubmitting(true)
    try {
      if (editing?.classifyId) {
        await classifyApi.updateClassify({ classifyId: editing.classifyId, name, description: values.description ?? '' })
        feedback.success(t('classify.updateSuccess'))
      } else {
        await classifyApi.createClassify({ type, name, description: values.description ?? '' })
        feedback.success(t('classify.addSuccess'))
      }
      reset()
      setModalOpen(false)
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setSubmitting(false)
    }
  }

  const handleDelete = async (record: Classify) => {
    if (!record.classifyId) return
    try {
      await classifyApi.deleteClassify(record.classifyId)
      feedback.success(t('classify.deleteSuccess'))
      if (editing?.classifyId === record.classifyId) reset()
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  return (
    <>
      <div style={{ display: 'flex', alignItems: 'center', gap: spacing.md, marginBottom: spacing.md }}>
        <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
          {t('classify.addClassify')}
        </Button>
        <Button icon={<ReloadOutlined />} onClick={load} loading={loading}>
          {t('ds.table.refresh')}
        </Button>
      </div>
      {loading ? (
        <div style={{ textAlign: 'center', padding: spacing.xxl * 2 }}>
          <Spin />
        </div>
      ) : items.length === 0 ? (
        <Empty description={t('classify.empty')} />
      ) : (
        <Row gutter={[16, 16]}>
          {items.map((item) => (
            <Col xs={24} sm={12} lg={8} xl={6} key={item.classifyId}>
              <ClassifyCardItem item={item} onEdit={openEdit} onDelete={(record) => void handleDelete(record)} />
            </Col>
          ))}
        </Row>
      )}
      <Modal
        open={modalOpen}
        title={editing ? t('classify.editClassify') : t('classify.addClassify')}
        onCancel={() => {
          reset()
          setModalOpen(false)
        }}
        onOk={handleOk}
        okText={t('classify.save')}
        confirmLoading={submitting}
        destroyOnClose
        maskClosable={false}
        width={480}
      >
        <Form form={form} layout="vertical">
          <Form.Item name="name" label={t('classify.name')} rules={[{ required: true, message: t('classify.nameRequired') }]}>
            <Input maxLength={20} />
          </Form.Item>
          <Form.Item name="description" label={t('classify.desc')}>
            <Input maxLength={255} />
          </Form.Item>
        </Form>
      </Modal>
    </>
  )
}

export function ClassifyPage() {
  const { t } = useTranslation()
  const isAdmin = useAppStore((state) => state.userInfo?.isAdmin === true)

  if (!isAdmin) {
    return <Navigate to="/dashboard" replace />
  }

  const items = TYPE_TABS.map((tab) => ({
    key: tab.key,
    label: t(tab.labelKey),
    children: <ClassifyPanel type={tab.key} />,
  }))

  return (
    <Page>
      <Tabs items={items} />
    </Page>
  )
}

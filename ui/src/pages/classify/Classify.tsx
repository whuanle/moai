import { useCallback, useEffect, useState } from 'react'
import { Button, Form, Input, Modal, Popconfirm, Space, Tabs } from 'antd'
import { DeleteOutlined, EditOutlined, PlusOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import { Navigate } from 'react-router'
import { classifyApi, ClassifyType, type Classify, type ClassifyTypeKey } from '@/api/classify'
import { DataTable, feedback, Page } from '@/design-system'
import { useAppStore } from '@/store/app'

const TYPE_TABS: { key: ClassifyTypeKey; labelKey: string }[] = [
  { key: ClassifyType.Plugin, labelKey: 'classify.typePlugin' },
  { key: ClassifyType.App, labelKey: 'classify.typeApp' },
  { key: ClassifyType.Kb, labelKey: 'classify.typeKb' },
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

  const columns = [
    { title: t('classify.colName'), dataIndex: 'name', width: 200 },
    { title: t('classify.colDesc'), dataIndex: 'description', ellipsis: true },
    { title: t('classify.colCreateUser'), dataIndex: 'createUserName', width: 120, render: (v: string | null) => v || '-' },
    { title: t('classify.colCreateTime'), dataIndex: 'createTime', width: 160, render: (v: string | null) => <span style={{ whiteSpace: 'nowrap' }}>{formatDateTime(v)}</span> },
    { title: t('classify.colUpdateUser'), dataIndex: 'updateUserName', width: 120, render: (v: string | null) => v || '-' },
    { title: t('classify.colUpdateTime'), dataIndex: 'updateTime', width: 160, render: (v: string | null) => <span style={{ whiteSpace: 'nowrap' }}>{formatDateTime(v)}</span> },
    {
      title: t('classify.colActions'),
      key: 'actions',
      width: 120,
      render: (_: unknown, record: Classify) => (
        <Space>
          <Button type="text" size="small" icon={<EditOutlined />} onClick={() => openEdit(record)} />
          <Popconfirm
            title={t('classify.deleteConfirm')}
            okButtonProps={{ danger: true }}
            onConfirm={() => void handleDelete(record)}
          >
            <Button type="text" size="small" danger icon={<DeleteOutlined />} />
          </Popconfirm>
        </Space>
      ),
    },
  ]

  return (
    <>
      <DataTable<Classify>
        rowKey="classifyId"
        columns={columns}
        dataSource={items}
        loading={loading}
        pagination={false}
        toolbar={
          <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
            {t('classify.addClassify')}
          </Button>
        }
        onRefresh={load}
        refreshLoading={loading}
      />
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

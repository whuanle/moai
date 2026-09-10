import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router'
import { Alert, Button, Descriptions, Form, Input, Popconfirm, Space } from 'antd'
import { Card, feedback } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { formatDateTime } from '@/utils/datetime'
import {
  deleteKnowledgeGraph,
  updateKnowledgeGraph,
  type KnowledgeGraphDetail as GraphDetail,
} from '@/api/knowledgeGraph'

const ROLE_MEMBER = 0

interface SettingsFormValues {
  name: string
  description?: string
}

interface KnowledgeGraphSettingsProps {
  graph: GraphDetail | null
  onChanged: () => void
}

export function KnowledgeGraphSettings({ graph, onChanged }: KnowledgeGraphSettingsProps) {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [saving, setSaving] = useState(false)
  const [form] = Form.useForm<SettingsFormValues>()

  const isAdminPlus = graph?.myRole != null && graph.myRole !== ROLE_MEMBER
  const isConnected = graph?.mode === 'connected'

  useEffect(() => {
    form.setFieldsValue({
      name: graph?.name ?? '',
      description: graph?.description ?? undefined,
    })
  }, [graph, form])

  const handleSave = async () => {
    if (!graph?.kgId) return
    const values = await form.validateFields()
    setSaving(true)
    try {
      await updateKnowledgeGraph(Number(graph.kgId), { name: values.name, description: values.description })
      feedback.success(t('knowledgegraph.saveSuccess'))
      onChanged()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setSaving(false)
    }
  }

  const handleDelete = async () => {
    if (!graph?.kgId) return
    try {
      await deleteKnowledgeGraph(Number(graph.kgId))
      feedback.success(t('knowledgegraph.deleteSuccess'))
      navigate('/kg')
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  if (graph === null) {
    return <Card loading styles={{ body: { padding: spacing.lg, maxWidth: 720, minHeight: 160 } }} />
  }

  return (
    <Card styles={{ body: { padding: spacing.lg, maxWidth: 720 } }}>
      <div style={{ fontWeight: 600, marginBottom: spacing.md }}>{t('knowledgegraph.settings.basicTitle')}</div>
      {isAdminPlus ? (
        <>
          <Form form={form} layout="vertical">
            <Form.Item
              name="name"
              label={t('knowledgegraph.name')}
              rules={[{ required: true, message: t('knowledgegraph.namePlaceholder') }]}
            >
              <Input maxLength={50} placeholder={t('knowledgegraph.namePlaceholder')} />
            </Form.Item>
            <Form.Item name="description" label={t('knowledgegraph.desc')}>
              <Input.TextArea maxLength={255} rows={3} />
            </Form.Item>
            <Button type="primary" loading={saving} onClick={() => void handleSave()}>
              {t('knowledgegraph.save')}
            </Button>
          </Form>
          <Descriptions column={1} style={{ marginTop: spacing.lg }}>
            {isConnected ? (
              <Descriptions.Item label={t('knowledgegraph.database')}>{graph?.database || '-'}</Descriptions.Item>
            ) : (
              <Descriptions.Item label={t('knowledgegraph.settings.templateKey')}>
                {graph?.templateKey || t('knowledgegraph.templateBlank')}
              </Descriptions.Item>
            )}
            <Descriptions.Item label={t('knowledgegraph.settings.createTime')}>
              {formatDateTime(graph?.createTime)}
            </Descriptions.Item>
          </Descriptions>
          <Space style={{ marginTop: spacing.lg }}>
            <Popconfirm
              title={isConnected ? t('knowledgegraph.deleteConnectedConfirm') : t('knowledgegraph.deleteConfirm')}
              okButtonProps={{ danger: true }}
              onConfirm={() => void handleDelete()}
            >
              <Button danger>{t('knowledgegraph.delete')}</Button>
            </Popconfirm>
          </Space>
        </>
      ) : (
        <Space direction="vertical" size={spacing.md}>
          <div>
            <span style={{ opacity: 0.65 }}>{t('knowledgegraph.name')}: </span>
            <span style={{ fontWeight: 600 }}>{graph?.name || '-'}</span>
          </div>
          <div>
            <span style={{ opacity: 0.65 }}>{t('knowledgegraph.desc')}: </span>
            <span style={{ fontWeight: 600 }}>{graph?.description || '-'}</span>
          </div>
          <Descriptions column={1}>
            {isConnected ? (
              <Descriptions.Item label={t('knowledgegraph.database')}>{graph?.database || '-'}</Descriptions.Item>
            ) : (
              <Descriptions.Item label={t('knowledgegraph.settings.templateKey')}>
                {graph?.templateKey || t('knowledgegraph.templateBlank')}
              </Descriptions.Item>
            )}
            <Descriptions.Item label={t('knowledgegraph.settings.createTime')}>
              {formatDateTime(graph?.createTime)}
            </Descriptions.Item>
          </Descriptions>
          <Alert type="warning" showIcon message={t('knowledgegraph.noPermission')} />
        </Space>
      )}
    </Card>
  )
}

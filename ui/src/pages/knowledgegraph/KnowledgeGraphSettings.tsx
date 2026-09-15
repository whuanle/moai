import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router'
import { Alert, Avatar, Button, Descriptions, Form, Input, Popconfirm, Space, Upload } from 'antd'
import type { UploadProps } from 'antd'
import { ClusterOutlined, UploadOutlined } from '@ant-design/icons'
import { Card, feedback } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { formatDateTime } from '@/utils/datetime'
import { resolveStorageUrl } from '@/utils/storage'
import {
  deleteKnowledgeGraph,
  updateKnowledgeGraph,
  uploadKnowledgeGraphAvatar,
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
  const [uploadingAvatar, setUploadingAvatar] = useState(false)
  const [form] = Form.useForm<SettingsFormValues>()

  const isAdminPlus = graph?.myRole != null && graph.myRole !== ROLE_MEMBER
  const isConnected = graph?.mode === 'connected'
  const avatarSrc = graph?.avatarPath?.trim() ? resolveStorageUrl(graph.avatarPath) : undefined

  const avatarBeforeUpload: UploadProps['beforeUpload'] = (file) => {
    if (!file.type.startsWith('image/')) {
      feedback.error(t('knowledgegraph.avatarTypeError'))
      return Upload.LIST_IGNORE
    }
    if (file.size > 5 * 1024 * 1024) {
      feedback.error(t('knowledgegraph.avatarSizeError'))
      return Upload.LIST_IGNORE
    }
    if (!graph?.kgId) return Upload.LIST_IGNORE
    setUploadingAvatar(true)
    uploadKnowledgeGraphAvatar(Number(graph.kgId), file)
      .then(() => feedback.success(t('knowledgegraph.avatarSuccess')))
      .then(() => onChanged())
      .catch(() => undefined)
      .finally(() => setUploadingAvatar(false))
    return Upload.LIST_IGNORE
  }

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
            <Form.Item label={t('knowledgegraph.avatar')}>
              <Space direction="vertical" size={spacing.xs}>
                <Space align="center">
                  <Upload beforeUpload={avatarBeforeUpload} showUploadList={false} accept="image/*" disabled={uploadingAvatar}>
                    <Avatar
                      // 加 key：上传成功后 src 变化时强制重挂载，避免 Avatar 缓存旧图
                      key={avatarSrc ?? 'default'}
                      shape="square"
                      size={64}
                      icon={!avatarSrc ? <ClusterOutlined /> : undefined}
                      src={avatarSrc}
                      style={{ cursor: 'pointer' }}
                    />
                  </Upload>
                  <Upload beforeUpload={avatarBeforeUpload} showUploadList={false} accept="image/*" disabled={uploadingAvatar}>
                    <Button icon={<UploadOutlined />} loading={uploadingAvatar}>
                      {avatarSrc ? t('knowledgegraph.avatarReplace') : t('knowledgegraph.avatar')}
                    </Button>
                  </Upload>
                </Space>
                <span style={{ opacity: 0.65 }}>{t('knowledgegraph.avatarHint')}</span>
              </Space>
            </Form.Item>
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

import { useCallback, useEffect, useState } from 'react'
import { AppstoreAddOutlined, SettingOutlined } from '@ant-design/icons'
import { Avatar, Button, Col, Empty, Form, Input, Modal, Row, Select, Space, Spin, Switch, Tag, Tooltip, Typography } from 'antd'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router'
import { AvatarUpload, Card, feedback } from '@/design-system'
import { neutralColors, spacing } from '@/design-system/theme'
import { resolveStorageUrl, uploadImageWithKey } from '@/utils/storage'
import { formatDateTime } from '@/utils/datetime'
import { createApp, getExternalApps, publishApp, unpublishApp, type AppItem, type AppKind } from '@/api/app'

const { Text, Paragraph } = Typography

const APP_KINDS: AppKind[] = ['agent', 'workflow']

const AVATAR_MAX_SIZE = 5 * 1024 * 1024

interface PendingAvatar {
  objectKey: string
  url: string
}

interface CreateFormValues {
  appType: AppKind
  name: string
  description?: string
  isAuth?: boolean
}

interface TeamExternalAppsProps {
  /** 所属团队 id */
  teamId: number
  /** 团队 Owner/Admin 才能创建与配置外部应用 */
  canManage: boolean
}

/**
 * 团队「外部应用」区块：创建供外部用户/匿名使用的外部应用，设置是否需要应用接入授权。
 * 平台内部用户在内部应用列表中看不到这些应用；配置（模型/提示词/插件/知识库）仍走应用管理页。
 */
export function TeamExternalApps({ teamId, canManage }: TeamExternalAppsProps) {
  const { t } = useTranslation()
  const navigate = useNavigate()

  const [loading, setLoading] = useState(true)
  const [items, setItems] = useState<AppItem[]>([])
  const [createOpen, setCreateOpen] = useState(false)
  const [submitting, setSubmitting] = useState(false)
  const [createAvatar, setCreateAvatar] = useState<PendingAvatar | null>(null)
  const [uploadingCreateAvatar, setUploadingCreateAvatar] = useState(false)
  const [busyId, setBusyId] = useState<string | null>(null)
  const [createForm] = Form.useForm<CreateFormValues>()

  const load = useCallback(async () => {
    if (!Number.isFinite(teamId) || teamId <= 0) return
    setLoading(true)
    try {
      const res = await getExternalApps(teamId)
      setItems(res.items ?? [])
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoading(false)
    }
  }, [teamId])

  useEffect(() => {
    void load()
  }, [load])

  const checkImage = (file: File) => {
    if (!file.type.startsWith('image/')) {
      feedback.error(t('appManage.avatarTypeError'))
      return false
    }
    if (file.size > AVATAR_MAX_SIZE) {
      feedback.error(t('appManage.avatarSizeError'))
      return false
    }
    return true
  }

  const openCreate = () => {
    createForm.resetFields()
    createForm.setFieldsValue({ appType: 'agent', isAuth: true })
    setCreateAvatar(null)
    setCreateOpen(true)
  }

  const handleCreate = async () => {
    const values = await createForm.validateFields()
    setSubmitting(true)
    try {
      await createApp({
        teamId,
        appType: values.appType,
        name: values.name,
        description: values.description,
        avatar: createAvatar?.objectKey,
        isExternal: true,
        isAuth: values.isAuth ?? false,
      })
      feedback.success(t('appManage.createSuccess'))
      setCreateOpen(false)
      setCreateAvatar(null)
      await load()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setSubmitting(false)
    }
  }

  const handleCreateAvatarFile = (file: File) => {
    if (!checkImage(file)) return
    setUploadingCreateAvatar(true)
    uploadImageWithKey(file)
      .then(({ objectKey, url }) => setCreateAvatar({ objectKey, url }))
      .catch(() => feedback.error(t('appManage.avatarUploadError')))
      .finally(() => setUploadingCreateAvatar(false))
  }

  const renderKind = (kind: AppItem['appType']) => {
    if (kind === 'workflow') return <Tag color="purple">{t('appManage.typeWorkflow')}</Tag>
    return <Tag color="blue">{t('appManage.typeAgent')}</Tag>
  }

  const openManage = (item: AppItem) => {
    if (!item.appId) return
    navigate(`/team/${teamId}/app/${item.appId}`)
  }

  const handleTogglePublish = async (item: AppItem) => {
    if (!item.appId) return
    setBusyId(item.appId)
    try {
      if (item.publishStatus === 1) {
        await unpublishApp(item.appId)
        feedback.success(t('appManage.unpublishSuccess'))
      } else {
        await publishApp(item.appId)
        feedback.success(t('appManage.publishSuccess'))
      }
      await load()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setBusyId(null)
    }
  }

  return (
    <>
      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          gap: spacing.sm,
          marginBottom: spacing.md,
        }}
      >
        <Text type="secondary" style={{ fontSize: 12 }}>
          {t('appManage.externalHint')}
        </Text>
        {canManage && (
          <Button type="primary" icon={<AppstoreAddOutlined />} onClick={openCreate}>
            {t('appManage.createExternal')}
          </Button>
        )}
      </div>

      <Spin spinning={loading}>
        {!loading && items.length === 0 ? (
          <Empty description={t('appManage.externalEmpty')} />
        ) : (
          <Row gutter={[spacing.md, spacing.md]}>
            {items.map((item) => {
              const name = item.name || '-'
              const published = item.publishStatus === 1
              return (
                <Col key={String(item.appId ?? '')} xs={24} sm={12} md={8} lg={6} xxl={4}>
                  <Card style={{ height: '100%' }} styles={{ body: { padding: spacing.md } }}>
                    <div style={{ display: 'flex', flexDirection: 'column', gap: spacing.sm, height: '100%' }}>
                      <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: spacing.sm }}>
                        <div style={{ display: 'flex', alignItems: 'center', gap: spacing.sm, minWidth: 0 }}>
                          <Avatar
                            shape="square"
                            size={44}
                            src={resolveStorageUrl(item.avatarPath ?? null) || undefined}
                            alt={name}
                          >
                            {name.slice(0, 1).toUpperCase()}
                          </Avatar>
                          <div style={{ minWidth: 0 }}>
                            <div
                              style={{
                                fontWeight: 600,
                                fontSize: 15,
                                lineHeight: 1.4,
                                whiteSpace: 'nowrap',
                                overflow: 'hidden',
                                textOverflow: 'ellipsis',
                              }}
                            >
                              {name}
                            </div>
                            <div style={{ marginTop: 2 }}>{renderKind(item.appType)}</div>
                          </div>
                        </div>
                        {canManage && (
                          <Button
                            type="link"
                            size="small"
                            icon={<SettingOutlined />}
                            style={{ height: 'auto', padding: 0, flexShrink: 0 }}
                            onClick={() => openManage(item)}
                          >
                            {t('appManage.manage')}
                          </Button>
                        )}
                      </div>
                      <Paragraph
                        type="secondary"
                        style={{ fontSize: 13, marginBottom: 0, minHeight: 38 }}
                        ellipsis={{ rows: 2 }}
                      >
                        {item.description || '-'}
                      </Paragraph>
                      {canManage && item.appType !== 'workflow' && (
                        <Button
                          size="small"
                          block
                          loading={busyId === item.appId}
                          type={published ? 'default' : 'primary'}
                          onClick={() => void handleTogglePublish(item)}
                        >
                          {published ? t('appManage.unpublish') : t('appManage.publish')}
                        </Button>
                      )}
                      <div
                        style={{
                          display: 'flex',
                          alignItems: 'center',
                          justifyContent: 'space-between',
                          gap: spacing.sm,
                          color: neutralColors.textTertiary,
                          fontSize: 12,
                          marginTop: 'auto',
                          borderTop: `1px solid ${neutralColors.border}`,
                          paddingTop: spacing.sm,
                        }}
                      >
                        <span>{formatDateTime(item.createTime)}</span>
                        <Space size={4}>
                          {item.appType !== 'workflow' && (
                            <Tag color={published ? 'green' : undefined} style={{ marginInlineEnd: 0 }}>
                              {published ? t('appManage.published') : t('appManage.unpublished')}
                            </Tag>
                          )}
                          <Tooltip title={t('appManage.isAuth')}>
                            <Tag color={item.isAuth ? 'gold' : 'cyan'} style={{ marginInlineEnd: 0 }}>
                              {item.isAuth ? t('appManage.authOn') : t('appManage.authOff')}
                            </Tag>
                          </Tooltip>
                        </Space>
                      </div>
                    </div>
                  </Card>
                </Col>
              )
            })}
          </Row>
        )}
      </Spin>

      <Modal
        open={createOpen}
        title={t('appManage.createExternalTitle')}
        onCancel={() => setCreateOpen(false)}
        onOk={() => void handleCreate()}
        confirmLoading={submitting}
        destroyOnHidden
        maskClosable={false}
        okText={t('appManage.confirm')}
        cancelText={t('appManage.cancel')}
      >
        <Form form={createForm} layout="vertical">
          <Form.Item
            name="appType"
            label={t('appManage.type')}
            rules={[{ required: true, message: t('appManage.typePlaceholder') }]}
          >
            <Select
              placeholder={t('appManage.typePlaceholder')}
              options={APP_KINDS.map((kind) => ({
                value: kind,
                label: kind === 'workflow' ? t('appManage.typeWorkflow') : t('appManage.typeAgent'),
              }))}
            />
          </Form.Item>
          <Form.Item label={t('appManage.avatar')}>
            <Space direction="vertical" size={spacing.xs}>
              <AvatarUpload
                src={createAvatar?.url || undefined}
                fallback={(createForm.getFieldValue('name') as string | undefined)?.slice(0, 1).toUpperCase() || '?'}
                shape="square"
                size={96}
                uploading={uploadingCreateAvatar}
                onSelect={handleCreateAvatarFile}
              />
              <Text type="secondary" style={{ fontSize: 12 }}>
                {t('appManage.avatarHint')}
              </Text>
            </Space>
          </Form.Item>
          <Form.Item
            name="name"
            label={t('appManage.name')}
            rules={[
              { required: true, message: t('appManage.namePlaceholder') },
              { max: 20, message: `${t('appManage.name')} ≤ 20` },
            ]}
          >
            <Input placeholder={t('appManage.namePlaceholder')} maxLength={20} />
          </Form.Item>
          <Form.Item name="description" label={t('appManage.description')} rules={[{ max: 255 }]}>
            <Input.TextArea placeholder={t('appManage.descriptionPlaceholder')} maxLength={255} rows={3} />
          </Form.Item>
          <Form.Item name="isAuth" label={t('appManage.isAuth')} valuePropName="checked">
            <Switch checkedChildren={t('appManage.authOn')} unCheckedChildren={t('appManage.authOff')} />
          </Form.Item>
          <Text type="secondary" style={{ fontSize: 12 }}>
            {t('appManage.isAuthHint')}
          </Text>
        </Form>
      </Modal>
    </>
  )
}

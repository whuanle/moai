import { useCallback, useEffect, useMemo, useState } from 'react'
import { AppstoreAddOutlined, SearchOutlined, SettingOutlined, UploadOutlined } from '@ant-design/icons'
import type { UploadProps } from 'antd'
import { Avatar, Button, Col, Empty, Form, Input, Modal, Row, Select, Space, Spin, Tag, Tooltip, Typography, Upload } from 'antd'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router'
import { Card, feedback, PageToolbar, useNeutralColors } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { resolveStorageUrl, uploadImageWithKey } from '@/utils/storage'
import { formatDateTime } from '@/utils/datetime'
import { NAME_PATTERN } from '@/utils/validation'
import { classifyApi, ClassifyType, classifyLabel, type Classify } from '@/api/classify'
import { createApp, getApps, type AppItem, type AppKind } from '@/api/app'
import { ExternalChannelEntry } from '@/components/ExternalChannelEntry'

const { Text, Paragraph } = Typography

const APP_KINDS: AppKind[] = ['agent', 'workflow']

/** 发布状态筛选：全部 / 已发布 / 未发布 */
type PublishFilter = 'all' | 'published' | 'unpublished'

/** 应用类型筛选：全部 / Agent / 流程 */
type TypeFilter = 'all' | AppKind

const AVATAR_MAX_SIZE = 5 * 1024 * 1024

/** 新建时已选中的头像：先走存储直传拿 objectKey，随创建请求一次性提交（应用此时还不存在，无法调头像接口） */
interface PendingAvatar {
  objectKey: string
  url: string
}

interface CreateFormValues {
  appType: AppKind
  name: string
  description?: string
  classifyId?: number
}

interface TeamAppsProps {
  /** 所属团队 id */
  teamId: number
  /** 团队 Owner/Admin 才能创建与配置应用；Member 只能查看与使用（镜像后端权限） */
  canManage: boolean
}

/**
 * 团队内的应用区块（挂载在团队管理页 TeamManage 的「应用」分区）。
 * 应用是团队下的产物：创建与配置都在团队内完成，不提供跨团队的全局应用入口。
 * 应用以卡片展示，卡片右上角「管理」进入应用管理页；顶部工具栏提供关键字/发布状态/类型筛选。
 */
export function TeamApps({ teamId, canManage }: TeamAppsProps) {
  const { t } = useTranslation()
  const neutral = useNeutralColors()
  const navigate = useNavigate()

  const [loading, setLoading] = useState(true)
  const [items, setItems] = useState<AppItem[]>([])
  // 工具栏筛选：关键字经「查询」按钮/回车生效，状态与类型下拉即时生效（均为客户端过滤）
  const [keyword, setKeyword] = useState('')
  const [appliedKeyword, setAppliedKeyword] = useState('')
  const [publishFilter, setPublishFilter] = useState<PublishFilter>('all')
  const [typeFilter, setTypeFilter] = useState<TypeFilter>('all')
  const [createOpen, setCreateOpen] = useState(false)
  const [submitting, setSubmitting] = useState(false)
  const [createAvatar, setCreateAvatar] = useState<PendingAvatar | null>(null)
  const [uploadingCreateAvatar, setUploadingCreateAvatar] = useState(false)
  const [classifies, setClassifies] = useState<Classify[]>([])
  const [createForm] = Form.useForm<CreateFormValues>()

  const classifyOptions = useMemo(
    () => classifies.map((c) => ({ value: Number(c.classifyId), label: classifyLabel(c) })),
    [classifies],
  )

  useEffect(() => {
    classifyApi
      .getClassifies(ClassifyType.App)
      .then(setClassifies)
      .catch(() => {
        // 错误已由全局请求中间件统一提示
      })
  }, [])

  const load = useCallback(async () => {
    if (!Number.isFinite(teamId) || teamId <= 0) return
    setLoading(true)
    try {
      const res = await getApps(teamId)
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

  /** 应用关键字筛选（查询按钮/回车触发） */
  const applySearch = useCallback(() => setAppliedKeyword(keyword.trim()), [keyword])

  /** 客户端过滤：关键字匹配名称/描述，叠加发布状态与应用类型 */
  const filteredItems = useMemo(() => {
    const kw = appliedKeyword.toLowerCase()
    return items.filter((item) => {
      if (kw && !`${item.name ?? ''}\n${item.description ?? ''}`.toLowerCase().includes(kw)) return false
      if (publishFilter === 'published' && item.publishStatus !== 1) return false
      if (publishFilter === 'unpublished' && item.publishStatus === 1) return false
      if (typeFilter !== 'all' && item.appType !== typeFilter) return false
      return true
    })
  }, [items, appliedKeyword, publishFilter, typeFilter])

  /** 图片前置校验（类型 + 大小 5MB），通过返回 true */
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
    createForm.setFieldsValue({ appType: 'agent' })
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
        classifyId: values.classifyId ?? 0,
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

  /** 新建弹窗：只上传换 objectKey，等提交创建时一并提交（此时应用还不存在） */
  const createAvatarBeforeUpload: UploadProps['beforeUpload'] = (file) => {
    if (!checkImage(file)) return Upload.LIST_IGNORE
    setUploadingCreateAvatar(true)
    uploadImageWithKey(file)
      .then(({ objectKey, url }) => setCreateAvatar({ objectKey, url }))
      .catch(() => feedback.error(t('appManage.avatarUploadError')))
      .finally(() => setUploadingCreateAvatar(false))
    return Upload.LIST_IGNORE
  }

  const renderKind = (kind: AppItem['appType']) => {
    if (kind === 'workflow') return <Tag color="purple">{t('appManage.typeWorkflow')}</Tag>
    return <Tag color="blue">{t('appManage.typeAgent')}</Tag>
  }

  const openManage = (item: AppItem) => {
    if (!item.appId) return
    navigate(`/team/${teamId}/app/${item.appId}`)
  }

  const openChat = (item: AppItem) => {
    if (!item.appId) return
    navigate(`/team/${teamId}/app/${item.appId}/chat`)
  }

  return (
    <>
      <PageToolbar
        filters={
          <Space size={spacing.xs} wrap>
            <Input
              value={keyword}
              onChange={(e) => setKeyword(e.target.value)}
              onPressEnter={applySearch}
              allowClear
              maxLength={100}
              prefix={<SearchOutlined style={{ color: 'inherit' }} />}
              placeholder={t('appManage.searchPlaceholder')}
              style={{ width: 240 }}
            />
            <Select<PublishFilter>
              value={publishFilter}
              onChange={setPublishFilter}
              style={{ width: 132 }}
              options={[
                { label: t('appManage.publishAll'), value: 'all' },
                { label: t('appManage.published'), value: 'published' },
                { label: t('appManage.unpublished'), value: 'unpublished' },
              ]}
            />
            <Select<TypeFilter>
              value={typeFilter}
              onChange={setTypeFilter}
              style={{ width: 132 }}
              options={[
                { label: t('appManage.typeAll'), value: 'all' },
                { label: t('appManage.typeAgent'), value: 'agent' },
                { label: t('appManage.typeWorkflow'), value: 'workflow' },
              ]}
            />
          </Space>
        }
        queries={
          <Button loading={loading} onClick={applySearch}>
            {t('ds.query.search')}
          </Button>
        }
        actions={
          canManage && (
            <Button type="primary" icon={<AppstoreAddOutlined />} onClick={openCreate}>
              {t('appManage.create')}
            </Button>
          )
        }
      />

      <Spin spinning={loading}>
        {!loading && items.length === 0 ? (
          <Empty description={t('appManage.empty')} />
        ) : !loading && filteredItems.length === 0 ? (
          <Empty description={t('appManage.filterEmpty')} />
        ) : (
          <Row gutter={[spacing.md, spacing.md]}>
            {filteredItems.map((item) => {
              const name = item.name || '-'
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
                        {/* 已开通外部渠道（如飞书）时在卡片右上角展示渠道入口，与管理入口并排 */}
                        <div style={{ display: 'flex', alignItems: 'flex-start', gap: spacing.xs, flexShrink: 0 }}>
                          <ExternalChannelEntry channels={item.externalChannels} />
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
                      </div>
                      <Paragraph
                        type="secondary"
                        style={{ fontSize: 13, marginBottom: 0, minHeight: 38 }}
                        ellipsis={{ rows: 2 }}
                      >
                        {item.description || '-'}
                      </Paragraph>
                      {item.publishStatus === 1 && (
                        <Button type="primary" size="small" block onClick={() => openChat(item)}>
                          {t('appManage.enterChat')}
                        </Button>
                      )}
                      <div
                        style={{
                          display: 'flex',
                          alignItems: 'center',
                          justifyContent: 'space-between',
                          gap: spacing.sm,
                          color: neutral.textTertiary,
                          fontSize: 12,
                          marginTop: 'auto',
                          borderTop: `1px solid ${neutral.border}`,
                          paddingTop: spacing.sm,
                        }}
                      >
                        <span>{formatDateTime(item.createTime)}</span>
                        <Space size={4}>
                          <Tag
                            color={item.publishStatus === 1 ? 'green' : undefined}
                            style={{ marginInlineEnd: 0 }}
                          >
                            {item.publishStatus === 1 ? t('appManage.published') : t('appManage.unpublished')}
                          </Tag>
                          <Tooltip title={t('appManage.isPublic')}>
                            <Tag color={item.isPublic ? 'green' : undefined} style={{ marginInlineEnd: 0 }}>
                              {item.isPublic ? t('appManage.publicOn') : t('appManage.publicOff')}
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
        title={t('appManage.createTitle')}
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
            <Space align="center">
              <Avatar shape="square" size={64} src={createAvatar?.url || undefined}>
                {(createForm.getFieldValue('name') as string | undefined)?.slice(0, 1).toUpperCase() || '?'}
              </Avatar>
              <Upload beforeUpload={createAvatarBeforeUpload} showUploadList={false} accept="image/*">
                <Button icon={<UploadOutlined />} loading={uploadingCreateAvatar}>
                  {t('appManage.avatarUpload')}
                </Button>
              </Upload>
            </Space>
            <div style={{ marginTop: spacing.xs }}>
              <Text type="secondary" style={{ fontSize: 12 }}>
                {t('appManage.avatarHint')}
              </Text>
            </div>
          </Form.Item>
          <Form.Item
            name="name"
            label={t('appManage.name')}
            rules={[
              { required: true, message: t('appManage.namePlaceholder') },
              { max: 20, message: `${t('appManage.name')} ≤ 20` },
              { pattern: NAME_PATTERN, message: t('validation.nameChars') },
            ]}
          >
            <Input placeholder={t('appManage.namePlaceholder')} maxLength={20} />
          </Form.Item>
          <Form.Item name="description" label={t('appManage.description')} rules={[{ max: 255 }]}>
            <Input.TextArea placeholder={t('appManage.descriptionPlaceholder')} maxLength={255} rows={3} />
          </Form.Item>
          <Form.Item name="classifyId" label={t('appManage.formClassify')}>
            <Select allowClear placeholder={t('appManage.classifyPlaceholder')} options={classifyOptions} />
          </Form.Item>
          <div style={{ marginTop: spacing.xs }}>
            <Text type="secondary" style={{ fontSize: 12 }}>
              {t('appManage.createHint')}
            </Text>
          </div>
        </Form>
      </Modal>
    </>
  )
}

import { useCallback, useEffect, useMemo, useState } from 'react'
import { Alert, Button, Col, Form, Input, Modal, Popconfirm, Row, Select, Space, Switch, Tag, Tooltip, Typography } from 'antd'
import { useTranslation } from 'react-i18next'
import { AvatarUpload, Card as DSCard, feedback } from '@/design-system'
import { fontSize, spacing } from '@/design-system/theme'
import { classifyApi, ClassifyType, classifyLabel, type Classify } from '@/api/classify'
import { updateApp, uploadAppAvatar } from '@/api/app'
import {
  applyPublication,
  getTeamPublicationList,
  withdrawPublication,
  type PublicationReviewItem,
} from '@/api/publication'
import { resolveStorageUrl } from '@/utils/storage'
import type { AppDetail } from './AppConfigSection'

const { Text } = Typography

interface InfoFormValues {
  name: string
  description?: string
  isAuth?: boolean
  classifyId?: number
}

export interface AppInfoSectionProps {
  teamId: number
  appId: string
  detail: AppDetail | null
  canManage: boolean
  onReload: () => Promise<void> | void
}

/**
 * 应用「信息」分区：应用基础信息维护（头像/类型/名称/描述/授权与公开状态）。
 * 应用公开（is_public）需系统管理员审批，团队侧可申请/撤回并查看审批状态。
 */
export function AppInfoSection({ teamId, appId, detail, canManage, onReload }: AppInfoSectionProps) {
  const { t } = useTranslation()

  const [savingInfo, setSavingInfo] = useState(false)
  const [uploadingAvatar, setUploadingAvatar] = useState(false)
  const [infoForm] = Form.useForm<InfoFormValues>()
  const [classifies, setClassifies] = useState<Classify[]>([])

  const isAgent = detail?.appType !== 'workflow'

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

  useEffect(() => {
    if (!detail) return
    infoForm.setFieldsValue({
      name: detail.name ?? '',
      description: detail.description ?? undefined,
      isAuth: detail.isAuth ?? false,
      classifyId: detail.classifyId || undefined,
    })
  }, [detail, infoForm])

  // 上架审核：应用公开（is_public）需系统管理员审批，团队侧可申请/撤回并查看审批状态
  const [publicationItems, setPublicationItems] = useState<PublicationReviewItem[]>([])
  const [publicationLoading, setPublicationLoading] = useState(false)
  const [applyOpen, setApplyOpen] = useState(false)
  const [applyReason, setApplyReason] = useState('')
  const [applying, setApplying] = useState(false)

  const pendingPublication = publicationItems.find((x) => x.state === 'pending')
  const lastRejected = publicationItems.find((x) => x.state === 'rejected')

  const loadPublications = useCallback(async () => {
    if (!Number.isFinite(teamId) || teamId <= 0) return
    setPublicationLoading(true)
    try {
      const list = await getTeamPublicationList(teamId, { resourceType: 'app' })
      setPublicationItems(list.filter((x) => x.resourceId === appId))
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setPublicationLoading(false)
    }
  }, [teamId, appId])

  useEffect(() => {
    void loadPublications()
  }, [loadPublications])

  const handleApplyPublication = async () => {
    setApplying(true)
    try {
      await applyPublication({
        resourceType: 'app',
        resourceId: appId,
        applyReason: applyReason.trim() || undefined,
      })
      feedback.success(t('appManage.applySuccess'))
      setApplyOpen(false)
      setApplyReason('')
      await loadPublications()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setApplying(false)
    }
  }

  const handleWithdrawPublication = async () => {
    if (!pendingPublication?.publicationId) return
    try {
      await withdrawPublication(pendingPublication.publicationId)
      feedback.success(t('appManage.withdrawSuccess'))
      await loadPublications()
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const handleSaveInfo = async () => {
    if (!appId) return
    const values = await infoForm.validateFields()
    setSavingInfo(true)
    try {
      await updateApp(appId, {
        name: values.name,
        description: values.description,
        isExternal: detail?.isExternal ?? false,
        isAuth: values.isAuth ?? false,
        classifyId: values.classifyId ?? 0,
      })
      feedback.success(t('appManage.updateSuccess'))
      await onReload()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setSavingInfo(false)
    }
  }

  /** 校验并上传应用头像；非法文件直接忽略 */
  const handleAvatarFile = (file: File) => {
    if (!file.type.startsWith('image/')) {
      feedback.error(t('appManage.avatarTypeError'))
      return
    }
    if (file.size > 5 * 1024 * 1024) {
      feedback.error(t('appManage.avatarSizeError'))
      return
    }
    if (!appId) return
    setUploadingAvatar(true)
    uploadAppAvatar(appId, file)
      .then(() => feedback.success(t('appManage.avatarSuccess')))
      .then(() => onReload())
      .catch(() => undefined)
      .finally(() => setUploadingAvatar(false))
  }

  const appName = detail?.name ?? ''

  return (
    <Row gutter={[spacing.md, spacing.md]} align="top">
      <Col xs={24} lg={15} xxl={16}>
        {!canManage && (
          <Alert
            type="info"
            showIcon
            message={t('appManage.memberHint')}
            style={{ marginBottom: spacing.md }}
          />
        )}
        <DSCard title={t('appManage.sectionInfo')}>
          <Form form={infoForm} layout="vertical" disabled={!canManage}>
            <Form.Item label={t('appManage.avatar')}>
              <Space direction="vertical" size={spacing.xs}>
                <AvatarUpload
                  src={resolveStorageUrl(detail?.avatarPath ?? null) || undefined}
                  fallback={appName.slice(0, 1).toUpperCase()}
                  shape="square"
                  size={96}
                  uploading={uploadingAvatar}
                  disabled={!canManage}
                  onSelect={handleAvatarFile}
                />
                <Text type="secondary" style={{ fontSize: fontSize.xs }}>
                  {t('appManage.avatarHint')}
                </Text>
              </Space>
            </Form.Item>
            <Form.Item label={t('appManage.type')}>
              {isAgent ? (
                <Tag color="blue">{t('appManage.typeAgent')}</Tag>
              ) : (
                <Tag color="purple">{t('appManage.typeWorkflow')}</Tag>
              )}
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
              <Input.TextArea placeholder={t('appManage.descriptionPlaceholder')} maxLength={255} rows={4} />
            </Form.Item>
            <Form.Item name="classifyId" label={t('appManage.formClassify')}>
              <Select allowClear placeholder={t('appManage.classifyPlaceholder')} options={classifyOptions} />
            </Form.Item>
            {detail?.isExternal ? (
              <Form.Item
                name="isAuth"
                label={t('appManage.isAuth')}
                valuePropName="checked"
                extra={t('appManage.isAuthHint')}
              >
                <Switch checkedChildren={t('appManage.authOn')} unCheckedChildren={t('appManage.authOff')} />
              </Form.Item>
            ) : (
              <Form.Item label={t('appManage.publicationStatus')} extra={t('appManage.publicationHint')}>
                {detail?.isPublic ? (
                  <Tag color="green">{t('appManage.publicOn')}</Tag>
                ) : pendingPublication ? (
                  <Space size={spacing.sm}>
                    <Tag color="orange">{t('appManage.publicationPending')}</Tag>
                    {canManage && (
                      <Popconfirm title={t('appManage.withdrawConfirm')} onConfirm={() => void handleWithdrawPublication()}>
                        <Button size="small" loading={publicationLoading}>
                          {t('appManage.withdrawApplication')}
                        </Button>
                      </Popconfirm>
                    )}
                  </Space>
                ) : (
                  <Space size={spacing.sm}>
                    {lastRejected && (
                      <Tooltip
                        title={
                          lastRejected.reviewComment
                            ? `${t('appManage.reviewCommentLabel')}: ${lastRejected.reviewComment}`
                            : undefined
                        }
                      >
                        <Tag color="error">{t('appManage.publicationRejected')}</Tag>
                      </Tooltip>
                    )}
                    {canManage ? (
                      <Button size="small" type="primary" onClick={() => setApplyOpen(true)}>
                        {lastRejected ? t('appManage.reapplyPublication') : t('appManage.applyPublication')}
                      </Button>
                    ) : (
                      <Tag>{t('appManage.publicOff')}</Tag>
                    )}
                  </Space>
                )}
              </Form.Item>
            )}
            {canManage && (
              <Button type="primary" loading={savingInfo} onClick={() => void handleSaveInfo()}>
                {t('appManage.saveInfo')}
              </Button>
            )}
          </Form>
        </DSCard>
      </Col>
      <Modal
        open={applyOpen}
        title={t('appManage.applyPublication')}
        onCancel={() => setApplyOpen(false)}
        confirmLoading={applying}
        onOk={() => void handleApplyPublication()}
        okText={t('appManage.applySubmit')}
        cancelText={t('appManage.cancel')}
        maskClosable={false}
        destroyOnHidden
      >
        <Text type="secondary" style={{ display: 'block', marginBottom: spacing.xs, fontSize: 12 }}>
          {t('appManage.applyHint')}
        </Text>
        <Input.TextArea
          value={applyReason}
          onChange={(e) => setApplyReason(e.target.value)}
          placeholder={t('appManage.applyReasonPlaceholder')}
          maxLength={255}
          rows={3}
        />
      </Modal>
    </Row>
  )
}

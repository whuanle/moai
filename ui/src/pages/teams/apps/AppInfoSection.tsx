import { useEffect, useMemo, useState } from 'react'
import { Alert, Button, Col, Form, Input, Popconfirm, Row, Select, Space, Switch, Tag, Typography } from 'antd'
import { useTranslation } from 'react-i18next'
import { AvatarUpload, Card as DSCard, feedback } from '@/design-system'
import { fontSize, spacing } from '@/design-system/theme'
import { classifyApi, ClassifyType, classifyLabel, type Classify } from '@/api/classify'
import { deleteApp, updateApp, uploadAppAvatar } from '@/api/app'
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
  appId: string
  detail: AppDetail | null
  canManage: boolean
  onReload: () => Promise<void> | void
  onDeleted: () => void
}

/**
 * 应用「信息」分区：应用基础信息维护（头像/类型/名称/描述/分类与授权开关）。
 * 上架（公开 is_public）的申请与审批状态已拆分至独立的「上架管理」分区（AppPublicationSection）。
 */
export function AppInfoSection({ appId, detail, canManage, onReload, onDeleted }: AppInfoSectionProps) {
  const { t } = useTranslation()

  const [savingInfo, setSavingInfo] = useState(false)
  const [deleting, setDeleting] = useState(false)
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

  const handleDelete = async () => {
    if (!appId || !canManage) return
    setDeleting(true)
    try {
      await deleteApp(appId)
      feedback.success(t('appManage.deleteSuccess'))
      onDeleted()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setDeleting(false)
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
            {detail?.isExternal && (
              <Form.Item
                name="isAuth"
                label={t('appManage.isAuth')}
                valuePropName="checked"
                extra={t('appManage.isAuthHint')}
              >
                <Switch checkedChildren={t('appManage.authOn')} unCheckedChildren={t('appManage.authOff')} />
              </Form.Item>
            )}
            {canManage && (
              <Space>
                <Button type="primary" loading={savingInfo} disabled={deleting} onClick={() => void handleSaveInfo()}>
                  {t('appManage.saveInfo')}
                </Button>
                <Popconfirm
                  title={t('appManage.deleteConfirm', { name: appName })}
                  description={t('appManage.deleteHint')}
                  onConfirm={handleDelete}
                  okText={t('appManage.delete')}
                  cancelText={t('appManage.cancel')}
                  okButtonProps={{ danger: true, loading: deleting }}
                >
                  <Button danger loading={deleting} disabled={savingInfo || uploadingAvatar}>
                    {t('appManage.delete')}
                  </Button>
                </Popconfirm>
              </Space>
            )}
          </Form>
        </DSCard>
      </Col>
    </Row>
  )
}

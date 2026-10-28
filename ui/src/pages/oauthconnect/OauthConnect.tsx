import { useEffect, useState } from 'react'
import { Button, Form, Input, Modal, Popconfirm, Select, Space, Tag, Tooltip, Typography, Upload } from 'antd'
import type { TableColumnsType } from 'antd'
import { DeleteOutlined, EditOutlined, PlusOutlined, UploadOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import { Navigate } from 'react-router'
import { Page, DataTable, feedback } from '@/design-system'
import { ImageSquareCropModal } from '@/components/ImageSquareCropModal'
import { useAppStore } from '@/store/app'
import type { OAuthPrivider } from '@/api/client/models'
import { resolveStorageUrl, uploadImage } from '@/utils/storage'
import {
  createOAuthConnection,
  deleteOAuthConnection,
  getOAuthConnections,
  updateOAuthConnection,
  type OAuthConnectionItem,
} from '@/api/oauthconnect'
import { formatDateTime } from '@/utils/datetime'

const { Text } = Typography


interface FormValues {
  name: string
  provider: string
  key: string
  secret?: string
  iconUrl: string
  wellKnown?: string
  agentId?: string
}

const providerTagColor: Record<string, string> = {
  custom: 'blue',
  feishu: 'geekblue',
  dingtalk: 'cyan',
  dingTalk: 'cyan',
  wechatwork: 'green',
  weixinWork: 'green',
  github: 'purple',
  gitHub: 'purple',
}

// 飞书、钉钉登录没有发现端点，默认图标地址（内置渠道逻辑）；企业微信无稳定公共图标源，由管理员上传
const providerDefaults: Record<string, { iconUrl?: string }> = {
  feishu: {
    iconUrl:
      'https://lf-package-cn.feishucdn.com/obj/feishu-static/lark/open/website/images/899fa60e60151c73aaea2e25871102dc.svg',
  },
  dingTalk: {
    iconUrl:
      'https://img.alicdn.com/imgextra/i1/O1CN01SNHEw41ysQFPN5Ql6_!!6000000006634-55-tps-176-31.svg',
  },
}

function providerLabel(provider: string | null | undefined): string {
  switch (provider) {
    case 'custom':
      return 'Custom OAuth'
    case 'feishu':
      return 'Feishu'
    case 'dingTalk':
    case 'dingtalk':
      return 'DingTalk'
    case 'wechatwork':
    case 'weixinWork':
      return 'WeCom'
    case 'gitHub':
    case 'github':
      return 'GitHub'
    default:
      return provider ?? '-'
  }
}

function providerNoDiscovery(provider: string | undefined): boolean {
  return provider === 'feishu' || provider === 'dingTalk' || provider === 'dingtalk' || provider === 'weixinWork'
}

interface IconPickerProps {
  value?: string
  onChange?: (value: string) => void
}

function IconPicker({ value, onChange }: IconPickerProps) {
  const { t } = useTranslation()
  const [uploading, setUploading] = useState(false)
  const [cropSource, setCropSource] = useState<{ file: File; url: string } | null>(null)
  const preview = resolveStorageUrl(value)

  // 选择图片后先进入正方形裁剪，确认再上传，保证登录页方形展示效果
  const handleFileSelected = (file: File) => {
    setCropSource({ file, url: URL.createObjectURL(file) })
  }

  const closeCrop = () => {
    setCropSource((current) => {
      if (current) URL.revokeObjectURL(current.url)
      return null
    })
  }

  const handleCropConfirm = async (cropped: File) => {
    closeCrop()
    setUploading(true)
    try {
      const objectKey = await uploadImage(cropped)
      onChange?.(objectKey)
    } catch {
      feedback.error(t('oauthconnect.iconUploadFailed'))
    } finally {
      setUploading(false)
    }
  }

  return (
    <Space style={{ width: '100%' }} direction="vertical" size={8}>
      <Space.Compact block>
        <Input
          value={value}
          placeholder="https://..."
          onChange={(e) => onChange?.(e.target.value)}
          aria-label={t('oauthconnect.icon')}
        />
        <Upload
          accept="image/*"
          showUploadList={false}
          customRequest={({ file }) => {
            if (file instanceof File) handleFileSelected(file)
          }}
        >
          <Button icon={<UploadOutlined />} loading={uploading}>
            {t('oauthconnect.uploadIcon')}
          </Button>
        </Upload>
      </Space.Compact>
      <Space size={12} align="center">
        {preview && (
          <img
            src={preview}
            alt={t('oauthconnect.icon')}
            style={{ width: 64, height: 64, borderRadius: 4, objectFit: 'contain' }}
          />
        )}
        <Text type="secondary" style={{ fontSize: 12 }}>
          {t('oauthconnect.iconSquareHint')}
        </Text>
      </Space>
      <ImageSquareCropModal
        open={cropSource !== null}
        src={cropSource?.url}
        fileName={cropSource?.file.name}
        onConfirm={(file) => void handleCropConfirm(file)}
        onCancel={closeCrop}
      />
    </Space>
  )
}

export function OauthConnect() {
  const { t } = useTranslation()
  const isAdmin = useAppStore((state) => state.userInfo?.isAdmin === true)
  const [form] = Form.useForm<FormValues>()
  const provider = Form.useWatch('provider', form)

  const [loading, setLoading] = useState(true)
  const [submitting, setSubmitting] = useState(false)
  const [items, setItems] = useState<OAuthConnectionItem[]>([])
  const [redirectUri, setRedirectUri] = useState('')
  const [modalOpen, setModalOpen] = useState(false)
  const [editing, setEditing] = useState<OAuthConnectionItem | null>(null)

  const load = async () => {
    setLoading(true)
    try {
      const res = await getOAuthConnections()
      setItems(res.items)
      setRedirectUri(res.redirectUri)
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    if (isAdmin) {
      void load()
    }
  }, [isAdmin])

  const openCreate = () => {
    setEditing(null)
    form.resetFields()
    setModalOpen(true)
  }

  const openEdit = (record: OAuthConnectionItem) => {
    setEditing(record)
    form.setFieldsValue({
      name: record.name ?? '',
      provider: record.provider ?? '',
      key: record.key ?? '',
      iconUrl: record.iconUrl ?? '',
      wellKnown: record.wellKnown ?? '',
      agentId: record.agentId ?? '',
      secret: '',
    })
    setModalOpen(true)
  }

  const handleProviderChange = (value: string) => {
    const defaults = providerDefaults[value]
    // 飞书、钉钉、企业微信没有发现端点，清空 wellKnown
    if (providerNoDiscovery(value)) {
      form.setFieldValue('wellKnown', undefined)
    }
    // 仅企业微信使用 agentId，切换提供商时清空
    if (value !== 'weixinWork') {
      form.setFieldValue('agentId', undefined)
    }
    // 自动填充内置渠道的默认图标（不覆盖已上传/已填写的图标）
    if (defaults?.iconUrl && !form.getFieldValue('iconUrl')) {
      form.setFieldValue('iconUrl', defaults.iconUrl)
    }
  }

  const handleOk = async () => {
    const values = await form.validateFields()
    setSubmitting(true)
    try {
      if (editing?.id) {
        await updateOAuthConnection(editing.id, {
          name: values.name,
          provider: values.provider as OAuthPrivider,
          key: values.key,
          secret: values.secret,
          iconUrl: values.iconUrl,
          wellKnown: values.wellKnown,
          agentId: values.agentId,
        })
      } else {
        await createOAuthConnection({
          name: values.name,
          provider: values.provider as OAuthPrivider,
          key: values.key,
          secret: values.secret ?? '',
          iconUrl: values.iconUrl,
          wellKnown: values.wellKnown,
          agentId: values.agentId,
        })
      }
      feedback.success(t('oauthconnect.saveSuccess'))
      setModalOpen(false)
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setSubmitting(false)
    }
  }

  const handleDelete = async (id: string) => {
    try {
      await deleteOAuthConnection(id)
      feedback.success(t('oauthconnect.deleteSuccess'))
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const providerOptions = [
    { value: 'custom', label: 'Custom OAuth' },
    { value: 'feishu', label: 'Feishu（飞书）' },
    { value: 'dingTalk', label: 'DingTalk（钉钉）' },
    { value: 'weixinWork', label: 'WeCom（企业微信）' }
  ]

  const columns: TableColumnsType<OAuthConnectionItem> = [
    { title: t('oauthconnect.colName'), dataIndex: 'name', width: 160 },
    {
      title: t('oauthconnect.colProvider'),
      dataIndex: 'provider',
      width: 140,
      render: (p: string | null) => (
        <Tag color={providerTagColor[p ?? '']}>{providerLabel(p)}</Tag>
      ),
    },
    { title: t('oauthconnect.colKey'), dataIndex: 'key', width: 220, ellipsis: true },
    {
      title: t('oauthconnect.colIcon'),
      dataIndex: 'iconUrl',
      width: 200,
      render: (url: string | null) => {
        const resolved = resolveStorageUrl(url)
        return resolved ? (
          <img
            src={resolved}
            alt="icon"
            style={{ height: 32, width: 200, borderRadius: 6, objectFit: 'contain' }}
          />
        ) : (
          '-'
        )
      },
    },
    {
      title: t('oauthconnect.colCallback'),
      key: 'redirectUri',
      ellipsis: true,
      render: () =>
        redirectUri ? (
          <Tooltip title={t('oauthconnect.callbackTip')}>
            <Text style={{ fontSize: 12 }} copyable>
              {redirectUri}
            </Text>
          </Tooltip>
        ) : (
          '-'
        ),
    },
    {
      title: t('oauthconnect.colCreateUser'),
      dataIndex: 'createUserName',
      width: 120,
      render: (v: string | null) => v || '-',
    },
    {
      title: t('oauthconnect.colCreateTime'),
      dataIndex: 'createTime',
      width: 160,
      render: (v: string | null) => <span style={{ whiteSpace: 'nowrap' }}>{formatDateTime(v)}</span>,
    },
    {
      title: t('oauthconnect.colUpdateUser'),
      dataIndex: 'updateUserName',
      width: 120,
      render: (v: string | null) => v || '-',
    },
    {
      title: t('oauthconnect.colUpdateTime'),
      dataIndex: 'updateTime',
      width: 160,
      render: (v: string | null) => <span style={{ whiteSpace: 'nowrap' }}>{formatDateTime(v)}</span>,
    },
    {
      title: t('oauthconnect.colActions'),
      key: 'actions',
      width: 110,
      fixed: 'right',
      render: (_, record) => (
        <Space size={0}>
          <Tooltip title={t('oauthconnect.edit')}>
            <Button
              type="text"
              size="small"
              icon={<EditOutlined />}
              aria-label={t('oauthconnect.edit')}
              onClick={() => openEdit(record)}
            />
          </Tooltip>
          <Popconfirm
            title={t('oauthconnect.deleteConfirm')}
            okButtonProps={{ danger: true }}
            onConfirm={() => record.id && handleDelete(record.id)}
          >
            <Tooltip title={t('oauthconnect.delete')}>
              <Button
                type="text"
                size="small"
                danger
                icon={<DeleteOutlined />}
                aria-label={t('oauthconnect.delete')}
              />
            </Tooltip>
          </Popconfirm>
        </Space>
      ),
    },
  ]

  if (!isAdmin) {
    return <Navigate to="/apps" replace />
  }

  return (
    <Page>
      <DataTable<OAuthConnectionItem>
        rowKey="id"
        columns={columns}
        dataSource={items}
        loading={loading}
        sticky
        scroll={{ x: 1440 }}
        toolbar={
          <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
            {t('oauthconnect.create')}
          </Button>
        }
        onRefresh={load}
        refreshLoading={loading}
        pagination={false}
      />
      <Modal
        open={modalOpen}
        title={editing ? t('oauthconnect.editTitle') : t('oauthconnect.createTitle')}
        onOk={handleOk}
        onCancel={() => setModalOpen(false)}
        okText={t('oauthconnect.save')}
        cancelText={t('oauthconnect.cancel')}
        confirmLoading={submitting}
        destroyOnClose
        maskClosable={false}
      >
        <Form form={form} layout="vertical" initialValues={{ provider: 'custom' }}>
          <Form.Item
            name="name"
            label={t('oauthconnect.colName')}
            rules={[{ required: true, message: t('oauthconnect.nameRequired') }]}
          >
            <Input maxLength={50} />
          </Form.Item>
          <Form.Item
            name="provider"
            label={t('oauthconnect.provider')}
            rules={[{ required: true }]}
            extra={
              redirectUri ? (
                provider === 'weixinWork' ? (
                  <Text
                    style={{ fontSize: 12 }}
                    copyable={{ text: new URL(redirectUri).host }}
                  >
                    {t('oauthconnect.callbackWeComTip', { host: new URL(redirectUri).host })}
                  </Text>
                ) : (
                  <Text
                    style={{ fontSize: 12 }}
                    copyable={{ text: redirectUri }}
                  >
                    {t('oauthconnect.callbackTip')}：{redirectUri}
                  </Text>
                )
              ) : undefined
            }
          >
            <Select options={providerOptions} disabled={Boolean(editing)} onChange={handleProviderChange} />
          </Form.Item>
          <Form.Item
            name="key"
            label={t('oauthconnect.key')}
            rules={[{ required: true, message: t('oauthconnect.keyRequired') }]}
          >
            <Input />
          </Form.Item>
          <Form.Item
            name="secret"
            label={t('oauthconnect.secret')}
            rules={editing ? [] : [{ required: true, message: t('oauthconnect.secretRequired') }]}
            extra={editing ? t('oauthconnect.secretKeep') : undefined}
          >
            <Input.Password />
          </Form.Item>
          <Form.Item
            name="iconUrl"
            label={t('oauthconnect.icon')}
            rules={[{ required: true, message: t('oauthconnect.iconRequired') }]}
          >
            <IconPicker />
          </Form.Item>
          {!providerNoDiscovery(provider) && (
            <Form.Item
              name="wellKnown"
              label={t('oauthconnect.wellKnown')}
              rules={[
                {
                  validator: (_, value) =>
                    !value
                      ? Promise.reject(new Error(t('oauthconnect.wellKnownRequired')))
                      : Promise.resolve(),
                },
              ]}
            >
              <Input placeholder="https://.../.well-known/openid-configuration" />
            </Form.Item>
          )}
          {provider === 'weixinWork' && (
            <Form.Item
              name="agentId"
              label={t('oauthconnect.agentId')}
              rules={[
                {
                  validator: (_, value) =>
                    !value
                      ? Promise.reject(new Error(t('oauthconnect.agentIdRequired')))
                      : Promise.resolve(),
                },
              ]}
              extra={t('oauthconnect.agentIdHint')}
            >
              <Input placeholder="1000002" />
            </Form.Item>
          )}
        </Form>
      </Modal>
    </Page>
  )
}

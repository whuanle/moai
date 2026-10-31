import { useEffect, useMemo, useState } from 'react'
import {
  Avatar,
  Button,
  Descriptions,
  Form,
  Input,
  Modal,
  Popconfirm,
  Select,
  Space,
  Tag,
  Tooltip,
  Typography,
  Upload,
} from 'antd'
import type { DescriptionsProps, TableColumnsType } from 'antd'
import {
  DeleteOutlined,
  EditOutlined,
  EyeOutlined,
  PlusOutlined,
  SearchOutlined,
  UploadOutlined,
} from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import { Navigate } from 'react-router'
import { AuditCell, Page, DataTable, QueryBar, feedback } from '@/design-system'
import { spacing } from '@/design-system/theme'
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

/** 方形图标 + 回退首字母（与用户页头像同语言） */
function ConnectionIcon({
  name,
  iconUrl,
  size,
}: {
  name: string
  iconUrl: string | null | undefined
  size: number
}) {
  return (
    <Avatar shape="square" size={size} src={iconUrl || undefined} alt={name}>
      {name.slice(0, 1).toUpperCase()}
    </Avatar>
  )
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
  const [detail, setDetail] = useState<OAuthConnectionItem | null>(null)
  const [detailOpen, setDetailOpen] = useState(false)
  const [keyword, setKeyword] = useState<string | undefined>(undefined)

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

  // 列表接口一次返回全部（不分页），关键字按名称/应用 Key 客户端过滤
  const filteredItems = useMemo(() => {
    const k = keyword?.trim().toLowerCase()
    if (!k) return items
    return items.filter(
      (it) => (it.name ?? '').toLowerCase().includes(k) || (it.key ?? '').toLowerCase().includes(k),
    )
  }, [items, keyword])

  const handleSearch = (values: Record<string, unknown>) => {
    const text = typeof values.searchText === 'string' ? values.searchText.trim() : ''
    setKeyword(text || undefined)
    // 查询即刷新：重拉列表并应用关键字过滤
    void load()
  }

  const handleReset = () => {
    setKeyword(undefined)
    void load()
  }

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

  const openDetail = (record: OAuthConnectionItem) => {
    setDetail(record)
    setDetailOpen(true)
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

  const renderProvider = (p: string | null | undefined) => (
    <Tag color={providerTagColor[p ?? '']}>{providerLabel(p)}</Tag>
  )

  // 全部列按百分比分配（操作列除外），表格永远等于容器宽度，不设 scroll.x：
  // 内容区窄（如 150% 缩放屏 ~980px）时按比例收缩，回调地址列吸收剩余宽度，不出现横向滚动
  const columns: TableColumnsType<OAuthConnectionItem> = useMemo(
    () => [
      {
        title: t('oauthconnect.colName'),
        key: 'name',
        width: '12%',
        ellipsis: true,
        render: (_, record) => (
          <Space>
            <ConnectionIcon
              name={record.name || '-'}
              iconUrl={resolveStorageUrl(record.iconUrl ?? null)}
              size={36}
            />
            <span>{record.name || '-'}</span>
          </Space>
        ),
      },
      {
        title: t('oauthconnect.colProvider'),
        dataIndex: 'provider',
        width: '13.5%',
        ellipsis: true,
        render: (p: string | null) => renderProvider(p),
      },
      { title: t('oauthconnect.colKey'), dataIndex: 'key', width: '11.5%', ellipsis: true },
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
        title: t('oauthconnect.colCreate'),
        key: 'create',
        width: '13.5%',
        render: (_, record) => <AuditCell user={record.createUserName} time={record.createTime} />,
      },
      {
        title: t('oauthconnect.colUpdate'),
        key: 'update',
        width: '13.5%',
        render: (_, record) => <AuditCell user={record.updateUserName} time={record.updateTime} />,
      },
      {
        title: t('oauthconnect.colActions'),
        key: 'actions',
        width: '22%',
        render: (_, record) => (
          <Space size={0}>
            <Button
              type="link"
              size="small"
              icon={<EyeOutlined />}
              aria-label={t('oauthconnect.view')}
              onClick={() => openDetail(record)}
            >
              {t('oauthconnect.view')}
            </Button>
            <Button
              type="link"
              size="small"
              icon={<EditOutlined />}
              aria-label={t('oauthconnect.edit')}
              onClick={() => openEdit(record)}
            >
              {t('oauthconnect.edit')}
            </Button>
            <Popconfirm
              title={t('oauthconnect.deleteConfirm')}
              okButtonProps={{ danger: true }}
              onConfirm={() => record.id && handleDelete(record.id)}
            >
              <Button
                type="link"
                size="small"
                danger
                icon={<DeleteOutlined />}
                aria-label={t('oauthconnect.delete')}
              >
                {t('oauthconnect.delete')}
              </Button>
            </Popconfirm>
          </Space>
        ),
      },
    ],
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [t, redirectUri],
  )

  const detailItems: DescriptionsProps['items'] = useMemo(() => {
    if (!detail) return []
    return [
      {
        key: 'provider',
        label: t('oauthconnect.colProvider'),
        children: renderProvider(detail.provider),
      },
      { key: 'key', label: t('oauthconnect.colKey'), children: detail.key || '-' },
      {
        key: 'callback',
        label: t('oauthconnect.colCallback'),
        children: redirectUri ? (
          <Text style={{ fontSize: 12 }} copyable>
            {redirectUri}
          </Text>
        ) : (
          '-'
        ),
      },
      {
        key: 'wellKnown',
        label: t('oauthconnect.wellKnown'),
        children: detail.wellKnown ? (
          <Text style={{ fontSize: 12 }} copyable>
            {detail.wellKnown}
          </Text>
        ) : (
          '-'
        ),
      },
      { key: 'agentId', label: t('oauthconnect.agentId'), children: detail.agentId || '-' },
      { key: 'createUser', label: t('oauthconnect.colCreateUser'), children: detail.createUserName || '-' },
      {
        key: 'createTime',
        label: t('oauthconnect.colCreateTime'),
        children: formatDateTime(detail.createTime),
      },
      { key: 'updateUser', label: t('oauthconnect.colUpdateUser'), children: detail.updateUserName || '-' },
      {
        key: 'updateTime',
        label: t('oauthconnect.colUpdateTime'),
        children: formatDateTime(detail.updateTime),
      },
    ]
  }, [detail, redirectUri, t])

  if (!isAdmin) {
    return <Navigate to="/apps" replace />
  }

  const detailName = detail?.name || '-'
  const detailIcon = resolveStorageUrl(detail?.iconUrl ?? null)

  return (
    <Page>
      {/* 查询即刷新：查询/重置/新建同一行，不再单设工具行与刷新按钮。
          连接器数量少，表格走自然文档流（不用 scroll.y 双表），tableLayout=fixed
          + 百分比列宽保证表头表体对齐且任意视口无横向溢出 */}
      <QueryBar
        onSearch={handleSearch}
        onReset={handleReset}
        loading={loading}
        extra={
          <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
            {t('oauthconnect.create')}
          </Button>
        }
      >
        <Form.Item name="searchText">
          <Input
            placeholder={t('oauthconnect.searchPlaceholder')}
            prefix={<SearchOutlined style={{ color: 'inherit' }} />}
            allowClear
            maxLength={50}
            style={{ width: 280 }}
          />
        </Form.Item>
      </QueryBar>
      <DataTable<OAuthConnectionItem>
        rowKey="id"
        columns={columns}
        dataSource={filteredItems}
        loading={loading}
        tableLayout="fixed"
        pagination={false}
      />
      <Modal
        open={detailOpen}
        title={t('oauthconnect.detailTitle')}
        footer={null}
        maskClosable={false}
        onCancel={() => setDetailOpen(false)}
      >
        {detail && (
          <>
            <div style={{ display: 'flex', alignItems: 'center', gap: spacing.md, marginBottom: spacing.lg }}>
              <ConnectionIcon name={detailName} iconUrl={detailIcon} size={56} />
              <div style={{ minWidth: 0 }}>
                <div style={{ fontSize: 16, fontWeight: 600 }}>{detailName}</div>
                <Text type="secondary">{providerLabel(detail.provider)}</Text>
              </div>
            </div>
            <Descriptions column={1} size="small" bordered items={detailItems} />
          </>
        )}
      </Modal>
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

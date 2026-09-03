import { useCallback, useEffect, useMemo, useState } from 'react'
import {
  CheckCircleOutlined,
  EyeOutlined,
  KeyOutlined,
  SearchOutlined,
  StopOutlined,
  UserSwitchOutlined,
} from '@ant-design/icons'
import { Avatar, Button, Descriptions, Form, Input, Modal, Popconfirm, Skeleton, Space, Tag, Tooltip, Typography } from 'antd'
import type { DescriptionsProps, TableColumnsType } from 'antd'
import { useTranslation } from 'react-i18next'
import { Navigate } from 'react-router'
import { Page, DataTable, QueryBar, feedback } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { useAppStore } from '@/store/app'
import { refreshUserProfile } from '@/api/auth'
import { resolveStorageUrl } from '@/utils/storage'
import { formatDateTime } from '@/utils/datetime'
import {
  getUsers,
  getUserDetail,
  resetUserPassword,
  setUserAdmin,
  setUserDisable,
  type UserListItem,
} from '@/api/usermanage'

const { Text } = Typography

interface PasswordFormValues {
  newPassword: string
  confirmPassword: string
}

const passwordPattern = /^(?=.*[A-Za-z])(?=.*\d).{8,20}$/


export function Users() {
  const { t } = useTranslation()
  const userInfo = useAppStore((state) => state.userInfo)
  const isAdmin = userInfo?.isAdmin === true
  const isRoot = userInfo?.isRoot === true
  const currentUserId = userInfo?.userId

  const [form] = Form.useForm<PasswordFormValues>()
  const [modalOpen, setModalOpen] = useState(false)
  const [detailOpen, setDetailOpen] = useState(false)
  const [detailLoading, setDetailLoading] = useState(false)
  const [detail, setDetail] = useState<Record<string, unknown> | null>(null)
  const [target, setTarget] = useState<UserListItem | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const [loading, setLoading] = useState(true)
  const [items, setItems] = useState<UserListItem[]>([])
  const [totalCount, setTotalCount] = useState(0)
  const [pageNo, setPageNo] = useState(1)
  const [pageSize, setPageSize] = useState(20)
  const [searchText, setSearchText] = useState<string | undefined>(undefined)

  const load = useCallback(async (page = pageNo, size = pageSize, keyword?: string) => {
    setLoading(true)
    try {
      const res = await getUsers({ pageNo: page, pageSize: size, searchText: keyword })
      setItems(res.items ?? [])
      setTotalCount(res.totalCount ?? 0)
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoading(false)
    }
  }, [pageNo, pageSize])

  useEffect(() => {
    if (isAdmin) {
      void load(1, pageSize, undefined)
      void refreshUserProfile().catch(() => undefined)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isAdmin])

  /** 目标行受保护：root 账号、自己、或（非 root 管理员看到的）其他管理员 */
  const isProtected = (record: UserListItem): boolean => {
    if (record.isRoot) return true
    if (String(record.id) === String(currentUserId)) return true
    if (record.isAdmin && !isRoot) return true
    return false
  }

  const handleSearch = (values: Record<string, unknown>) => {
    const keyword = typeof values.searchText === 'string' ? values.searchText.trim() : undefined
    setSearchText(keyword || undefined)
    setPageNo(1)
    void load(1, pageSize, keyword || undefined)
  }

  const handleReset = () => {
    setSearchText(undefined)
    setPageNo(1)
    void load(1, pageSize, undefined)
  }

  const openDetail = async (record: UserListItem) => {
    setDetail(null)
    setDetailLoading(true)
    setDetailOpen(true)
    try {
      const res = await getUserDetail(Number(record.id))
      // 详情接口无 createTime，沿用列表行数据补齐
      setDetail({ ...res, createTime: record.createTime } as unknown as Record<string, unknown>)
    } catch {
      setDetailOpen(false)
      // 错误已由全局请求中间件统一提示
    } finally {
      setDetailLoading(false)
    }
  }

  const handleToggleAdmin = async (record: UserListItem) => {
    try {
      await setUserAdmin(Number(record.id), !record.isAdmin)
      feedback.success(t(record.isAdmin ? 'users.unsetAdminSuccess' : 'users.setAdminSuccess'))
      void load(pageNo, pageSize, searchText)
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const handleToggleDisable = async (record: UserListItem) => {
    try {
      await setUserDisable(Number(record.id), !record.isDisable)
      feedback.success(t(record.isDisable ? 'users.enableSuccess' : 'users.disableSuccess'))
      void load(pageNo, pageSize, searchText)
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const handleResetPassword = async () => {
    const values = await form.validateFields()
    setSubmitting(true)
    try {
      await resetUserPassword(Number(target?.id), values.newPassword)
      feedback.success(t('users.resetPasswordSuccess'))
      setModalOpen(false)
      form.resetFields()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setSubmitting(false)
    }
  }

  const renderRole = (record: UserListItem) => {
    if (record.isRoot) return <Tag color="gold">{t('users.roleRoot')}</Tag>
    if (record.isAdmin) return <Tag color="blue">{t('users.roleAdmin')}</Tag>
    return <Tag>{t('users.roleMember')}</Tag>
  }

  const renderStatus = (record: UserListItem) =>
    record.isDisable ? (
      <Tag color="error">{t('users.statusDisabled')}</Tag>
    ) : (
      <Tag color="success">{t('users.statusNormal')}</Tag>
    )

  /** 头像 + 用户名 + 昵称合并展示，释放列宽给邮箱等长文本列 */
  const renderUser = (record: UserListItem) => {
    const userName = record.userName || '-'
    const nickName = record.nickName?.trim()
    const avatarUrl = resolveStorageUrl(record.avatar ?? null)
    return (
      <Space>
        <Avatar size={36} src={avatarUrl || undefined} alt={userName}>
          {userName.slice(0, 1).toUpperCase()}
        </Avatar>
        <div style={{ lineHeight: 1.4, minWidth: 0 }}>
          <div>{userName}</div>
          {nickName && nickName !== userName && (
            <Text type="secondary" style={{ fontSize: 12 }}>
              {nickName}
            </Text>
          )}
        </div>
      </Space>
    )
  }

  const columns: TableColumnsType<UserListItem> = useMemo(
    () => [
      {
        title: t('users.colUser'),
        key: 'user',
        width: 160,
        render: (_, record) => renderUser(record),
      },
      {
        title: t('users.colEmail'),
        dataIndex: 'email',
        ellipsis: true,
        render: (v: string | null) => v || '-',
      },
      { title: t('users.colPhone'), dataIndex: 'phone', width: 130, render: (v: string | null) => v || '-' },
      { title: t('users.colRole'), key: 'role', width: 100, render: (_, record) => renderRole(record) },
      { title: t('users.colStatus'), key: 'status', width: 75, render: (_, record) => renderStatus(record) },
      {
        title: t('users.colCreateTime'),
        dataIndex: 'createTime',
        width: 150,
        render: (v: string | null) => (
          <span style={{ whiteSpace: 'nowrap' }}>{formatDateTime(v)}</span>
        ),
      },
      {
        title: t('users.colActions'),
        key: 'actions',
        width: 160,
        fixed: 'right',
        render: (_, record) => {
          const protectedRow = isProtected(record)
          return (
            <Space size={0}>
              <Tooltip title={t('users.view')}>
                <Button
                  type="text"
                  size="small"
                  icon={<EyeOutlined />}
                  aria-label={t('users.view')}
                  onClick={() => void openDetail(record)}
                />
              </Tooltip>
              {isRoot && !protectedRow && (
                <Popconfirm
                  title={t(record.isAdmin ? 'users.unsetAdminConfirm' : 'users.setAdminConfirm')}
                  onConfirm={() => void handleToggleAdmin(record)}
                >
                  <Tooltip title={t(record.isAdmin ? 'users.unsetAdmin' : 'users.setAdmin')}>
                    <Button
                      type="text"
                      size="small"
                      icon={<UserSwitchOutlined />}
                      aria-label={t(record.isAdmin ? 'users.unsetAdmin' : 'users.setAdmin')}
                    />
                  </Tooltip>
                </Popconfirm>
              )}
              {!protectedRow && (
                <Popconfirm
                  title={t(record.isDisable ? 'users.enableConfirm' : 'users.disableConfirm')}
                  okButtonProps={{ danger: !record.isDisable }}
                  onConfirm={() => void handleToggleDisable(record)}
                >
                  <Tooltip title={t(record.isDisable ? 'users.enable' : 'users.disable')}>
                    <Button
                      type="text"
                      size="small"
                      danger={!record.isDisable}
                      icon={record.isDisable ? <CheckCircleOutlined /> : <StopOutlined />}
                      aria-label={t(record.isDisable ? 'users.enable' : 'users.disable')}
                    />
                  </Tooltip>
                </Popconfirm>
              )}
              {!protectedRow && (
                <Tooltip title={t('users.resetPassword')}>
                  <Button
                    type="text"
                    size="small"
                    icon={<KeyOutlined />}
                    aria-label={t('users.resetPassword')}
                    onClick={() => {
                      setTarget(record)
                      form.resetFields()
                      setModalOpen(true)
                    }}
                  />
                </Tooltip>
              )}
            </Space>
          )
        },
      },
    ],
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [t, isRoot, currentUserId, searchText, pageNo, pageSize],
  )

  const detailItems: DescriptionsProps['items'] = useMemo(() => {
    if (!detail) return []
    return [
      { key: 'email', label: t('users.colEmail'), children: String(detail.email ?? '-') },
      { key: 'phone', label: t('users.colPhone'), children: String(detail.phone ?? '-') },
      {
        key: 'role',
        label: t('users.colRole'),
        children: detail.isRoot
          ? t('users.roleRoot')
          : detail.isAdmin
            ? t('users.roleAdmin')
            : t('users.roleMember'),
      },
      {
        key: 'status',
        label: t('users.colStatus'),
        children: detail.isDisable ? t('users.statusDisabled') : t('users.statusNormal'),
      },
      {
        key: 'createTime',
        label: t('users.colCreateTime'),
        children: formatDateTime(detail.createTime as string | null),
      },
    ]
  }, [detail, t])

  if (!isAdmin) {
    return <Navigate to="/dashboard" replace />
  }

  const detailUserName = String(detail?.userName ?? '')
  const detailNickName = String(detail?.nickName ?? '')
  const detailAvatar = resolveStorageUrl((detail?.avatar as string | null | undefined) ?? null)

  return (
    <Page>
      <QueryBar onSearch={handleSearch} onReset={handleReset} loading={loading}>
        <Form.Item name="searchText">
          <Input
            placeholder={t('users.searchPlaceholder')}
            prefix={<SearchOutlined style={{ color: 'inherit' }} />}
            allowClear
            maxLength={50}
            style={{ width: 280 }}
          />
        </Form.Item>
      </QueryBar>
      <DataTable<UserListItem>
        rowKey="id"
        columns={columns}
        dataSource={items}
        loading={loading}
        sticky
        scroll={{ x: 880 }}
        toolbar={
          <Text type="secondary">{t('ds.table.total', { total: totalCount })}</Text>
        }
        onRefresh={() => void load(pageNo, pageSize, searchText)}
        refreshLoading={loading}
        pagination={{
          current: pageNo,
          pageSize,
          total: totalCount,
          showSizeChanger: true,
          onChange: (page, size) => {
            setPageNo(page)
            setPageSize(size)
            void load(page, size, searchText)
          },
        }}
      />
      <Modal
        open={detailOpen}
        title={t('users.detailTitle')}
        footer={null}
        onCancel={() => setDetailOpen(false)}
      >
        <Skeleton loading={detailLoading} active paragraph={{ rows: 5 }}>
          {detail && (
            <>
              <div style={{ display: 'flex', alignItems: 'center', gap: spacing.md, marginBottom: spacing.lg }}>
                <Avatar size={56} src={detailAvatar || undefined} alt={detailUserName}>
                  {detailUserName.slice(0, 1).toUpperCase()}
                </Avatar>
                <div style={{ minWidth: 0 }}>
                  <div style={{ fontSize: 16, fontWeight: 600 }}>{detailNickName || detailUserName}</div>
                  {detailNickName && detailNickName !== detailUserName && (
                    <Text type="secondary">@{detailUserName}</Text>
                  )}
                </div>
              </div>
              <Descriptions column={1} size="small" bordered items={detailItems} />
            </>
          )}
        </Skeleton>
      </Modal>
      <Modal
        open={modalOpen}
        title={t('users.resetPasswordTitle', {
          name: target?.userName ?? '',
        })}
        onOk={() => void handleResetPassword()}
        onCancel={() => setModalOpen(false)}
        okText={t('users.confirm')}
        cancelText={t('users.cancel')}
        confirmLoading={submitting}
        destroyOnHidden
        maskClosable={false}
      >
        <Form form={form} layout="vertical">
          <Form.Item
            name="newPassword"
            label={t('users.newPassword')}
            rules={[
              { required: true, message: t('users.newPasswordRequired') },
              {
                pattern: passwordPattern,
                message: t('users.passwordRule'),
              },
            ]}
          >
            <Input.Password autoComplete="new-password" maxLength={20} />
          </Form.Item>
          <Form.Item
            name="confirmPassword"
            label={t('users.confirmPassword')}
            dependencies={['newPassword']}
            rules={[
              { required: true, message: t('users.confirmPasswordRequired') },
              ({ getFieldValue }) => ({
                validator(_, value) {
                  if (!value || getFieldValue('newPassword') === value) {
                    return Promise.resolve()
                  }
                  return Promise.reject(new Error(t('users.passwordMismatch')))
                },
              }),
            ]}
          >
            <Input.Password autoComplete="new-password" maxLength={20} />
          </Form.Item>
          <Text type="secondary">{t('users.passwordRule')}</Text>
        </Form>
      </Modal>
    </Page>
  )
}

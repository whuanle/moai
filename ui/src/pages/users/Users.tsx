import { useCallback, useEffect, useMemo, useState } from 'react'
import { Button, Descriptions, Form, Input, Modal, Popconfirm, Space, Tag, Typography } from 'antd'
import type { TableColumnsType } from 'antd'
import { useTranslation } from 'react-i18next'
import { Navigate } from 'react-router'
import { Page, DataTable, QueryBar, feedback } from '@/design-system'
import { useAppStore } from '@/store/app'
import { refreshUserProfile } from '@/api/auth'
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
    try {
      const res = await getUserDetail(Number(record.id))
      setDetail(res as unknown as Record<string, unknown>)
      setDetailOpen(true)
    } catch {
      // 错误已由全局请求中间件统一提示
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

  const columns: TableColumnsType<UserListItem> = useMemo(
    () => [
      { title: t('users.colUserName'), dataIndex: 'userName', width: 140 },
      { title: t('users.colNickName'), dataIndex: 'nickName', width: 140 },
      { title: t('users.colEmail'), dataIndex: 'email', ellipsis: true },
      { title: t('users.colPhone'), dataIndex: 'phone', width: 140, render: (v: string | null) => v || '-' },
      { title: t('users.colRole'), key: 'role', width: 110, render: (_, record) => renderRole(record) },
      { title: t('users.colStatus'), key: 'status', width: 90, render: (_, record) => renderStatus(record) },
      {
        title: t('users.colCreateTime'),
        dataIndex: 'createTime',
        width: 170,
        render: (v: string | null) => (v ? new Date(v).toLocaleString() : '-'),
      },
      {
        title: t('users.colActions'),
        key: 'actions',
        width: 240,
        render: (_, record) => {
          const protectedRow = isProtected(record)
          return (
            <Space size={0} wrap>
              <Button type="link" size="small" onClick={() => void openDetail(record)}>
                {t('users.view')}
              </Button>
              {isRoot && !protectedRow && (
                <Popconfirm
                  title={t(record.isAdmin ? 'users.unsetAdminConfirm' : 'users.setAdminConfirm')}
                  onConfirm={() => void handleToggleAdmin(record)}
                >
                  <Button type="link" size="small">
                    {t(record.isAdmin ? 'users.unsetAdmin' : 'users.setAdmin')}
                  </Button>
                </Popconfirm>
              )}
              {!protectedRow && (
                <Popconfirm
                  title={t(record.isDisable ? 'users.enableConfirm' : 'users.disableConfirm')}
                  onConfirm={() => void handleToggleDisable(record)}
                >
                  <Button type="link" size="small" danger={!record.isDisable}>
                    {t(record.isDisable ? 'users.enable' : 'users.disable')}
                  </Button>
                </Popconfirm>
              )}
              {!protectedRow && (
                <Button
                  type="link"
                  size="small"
                  onClick={() => {
                    setTarget(record)
                    form.resetFields()
                    setModalOpen(true)
                  }}
                >
                  {t('users.resetPassword')}
                </Button>
              )}
            </Space>
          )
        },
      },
    ],
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [t, isRoot, currentUserId],
  )

  if (!isAdmin) {
    return <Navigate to="/dashboard" replace />
  }

  return (
    <Page title={t('users.title')} subtitle={t('users.subtitle')}>
      <QueryBar onSearch={handleSearch} onReset={handleReset} loading={loading}>
        <Form.Item name="searchText" label={t('users.searchLabel')}>
          <Input placeholder={t('users.searchPlaceholder')} allowClear maxLength={50} />
        </Form.Item>
      </QueryBar>
      <DataTable<UserListItem>
        rowKey="id"
        columns={columns}
        dataSource={items}
        loading={loading}
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
        {detail && (
          <Descriptions column={1} size="small" bordered>
            <Descriptions.Item label={t('users.colUserName')}>
              {String(detail.userName ?? '-')}
            </Descriptions.Item>
            <Descriptions.Item label={t('users.colNickName')}>
              {String(detail.nickName ?? '-')}
            </Descriptions.Item>
            <Descriptions.Item label={t('users.colEmail')}>
              {String(detail.email ?? '-')}
            </Descriptions.Item>
            <Descriptions.Item label={t('users.colPhone')}>
              {String(detail.phone ?? '-')}
            </Descriptions.Item>
            <Descriptions.Item label={t('users.colRole')}>
              {detail.isRoot
                ? t('users.roleRoot')
                : detail.isAdmin
                  ? t('users.roleAdmin')
                  : t('users.roleMember')}
            </Descriptions.Item>
            <Descriptions.Item label={t('users.colStatus')}>
              {detail.isDisable ? t('users.statusDisabled') : t('users.statusNormal')}
            </Descriptions.Item>
          </Descriptions>
        )}
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

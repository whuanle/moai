import { useEffect, useState } from 'react'
import { Avatar, Button, Form, Input, Popconfirm, Typography, Upload } from 'antd'
import { LinkOutlined, UploadOutlined } from '@ant-design/icons'
import type { UploadProps } from 'antd'
import { useTranslation } from 'react-i18next'
import { Card, Page, feedback } from '@/design-system'
import { useAppStore } from '@/store/app'
import { resolveStorageUrl } from '@/utils/storage'
import { getOAuthProviders, refreshUserProfile, type OAuthProviderItem } from '@/api/auth'
import {
  getBoundAccounts,
  resetPassword,
  unbindProvider,
  updateUserInfo,
  uploadAvatar,
  type AccountBoundItem,
} from '@/api/account'
import { toBindAuthorizeUrl } from '@/utils/oauth'

const { Text, Title } = Typography

interface BasicFormValues {
  nickName?: string
  phone?: string
}

interface PasswordFormValues {
  oldPassword: string
  newPassword: string
  confirmPassword: string
}

const PASSWORD_RULE = /^(?=.*[A-Za-z])(?=.*\d)[A-Za-z\d\S]{8,20}$/

export function AccountSettings() {
  const { t } = useTranslation()
  const userInfo = useAppStore((state) => state.userInfo)
  const [basicForm] = Form.useForm<BasicFormValues>()
  const [passwordForm] = Form.useForm<PasswordFormValues>()

  const [loading, setLoading] = useState(true)
  const [basicLoading, setBasicLoading] = useState(false)
  const [passwordLoading, setPasswordLoading] = useState(false)
  const [avatarLoading, setAvatarLoading] = useState(false)
  const [providers, setProviders] = useState<OAuthProviderItem[]>([])
  const [boundAccounts, setBoundAccounts] = useState<AccountBoundItem[]>([])

  const avatar = resolveStorageUrl(userInfo?.avatar)
  const displayName = userInfo?.nickName ?? userInfo?.userName

  const load = async () => {
    setLoading(true)
    try {
      const [providerItems, bound] = await Promise.all([getOAuthProviders(), getBoundAccounts()])
      setProviders(providerItems)
      setBoundAccounts(bound)
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    void load()
  }, [])

  // 监听绑定弹窗的回调结果：成功后刷新已绑定列表。
  useEffect(() => {
    const onMessage = (e: MessageEvent) => {
      if (e.origin !== window.location.origin) return
      const data = e.data as { type?: string } | null
      if (data?.type === 'oauth_bind_success') {
        feedback.success(t('account.bindSuccess'))
        void load()
      } else if (data?.type === 'oauth_bind_error') {
        const message = (data as { message?: string | null }).message
        feedback.error(message ?? t('account.bindError'))
      }
    }
    window.addEventListener('message', onMessage)
    return () => window.removeEventListener('message', onMessage)
  }, [t])

  useEffect(() => {
    basicForm.setFieldsValue({
      nickName: userInfo?.nickName ?? undefined,
      phone: userInfo?.phone ?? undefined,
    })
  }, [userInfo?.nickName, userInfo?.phone, basicForm])

  const handleBasicSubmit = async (values: BasicFormValues) => {
    setBasicLoading(true)
    try {
      await updateUserInfo({ nickName: values.nickName, phone: values.phone })
      await refreshUserProfile()
      feedback.success(t('account.saveSuccess'))
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setBasicLoading(false)
    }
  }

  const handlePasswordSubmit = async (values: PasswordFormValues) => {
    setPasswordLoading(true)
    try {
      await resetPassword(values.oldPassword, values.newPassword)
      passwordForm.resetFields()
      feedback.success(t('account.passwordSuccess'))
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setPasswordLoading(false)
    }
  }

  const beforeUpload: UploadProps['beforeUpload'] = (file) => {
    const isImage = file.type.startsWith('image/')
    if (!isImage) {
      feedback.error(t('account.avatarTypeError'))
      return Upload.LIST_IGNORE
    }
    if (file.size > 5 * 1024 * 1024) {
      feedback.error(t('account.avatarSizeError'))
      return Upload.LIST_IGNORE
    }
    return true
  }

  const handleAvatarUpload: UploadProps['customRequest'] = async ({ file }) => {
    if (!(file instanceof File)) return
    setAvatarLoading(true)
    try {
      await uploadAvatar(file)
      await refreshUserProfile()
      feedback.success(t('account.avatarSuccess'))
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setAvatarLoading(false)
    }
  }

  const handleUnbind = async (oAuthId: string) => {
    try {
      await unbindProvider(oAuthId)
      feedback.success(t('account.unbindSuccess'))
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const isBound = (oAuthId?: string | null) => boundAccounts.some((b) => b.oAuthId === oAuthId)

  const handleBind = (provider: OAuthProviderItem) => {
    const bindUrl = toBindAuthorizeUrl(provider.redirectUrl)
    if (!bindUrl) return
    window.open(bindUrl, 'oauth_bind', 'width=600,height=750,menubar=no,toolbar=no,location=no,status=no,scrollbars=no')
  }

  const providerList = (providers ?? []).map((provider) => {
    const bound = isBound(provider.oAuthId)
    return { provider, bound }
  })

  return (
    <Page title={t('account.title')} subtitle={t('account.subtitle')}>
      <div style={{ maxWidth: 720, margin: '0 auto', display: 'flex', flexDirection: 'column', gap: 24 }}>
        <Card>
          <div style={{ display: 'flex', alignItems: 'center', gap: 16 }}>
            <Avatar size={72} src={avatar || undefined} icon={<UploadOutlined />}>
              {displayName?.charAt(0) ?? 'U'}
            </Avatar>
            <div style={{ flex: 1, minWidth: 0 }}>
              <Title level={5} style={{ margin: 0 }}>
                {displayName}
              </Title>
              <Text type="secondary">{userInfo?.email ?? userInfo?.userName}</Text>
            </div>
            <Upload
              accept="image/*"
              showUploadList={false}
              beforeUpload={beforeUpload}
              customRequest={handleAvatarUpload}
            >
              <Button icon={<UploadOutlined />} loading={avatarLoading}>
                {t('account.uploadAvatar')}
              </Button>
            </Upload>
          </div>
        </Card>

        <Card title={t('account.basicTitle')}>
          <Form<BasicFormValues> form={basicForm} layout="vertical" onFinish={handleBasicSubmit}>
            <Form.Item
              name="nickName"
              label={t('auth.nickName')}
              rules={[{ required: true, message: t('auth.nickNamePlaceholder') }]}
            >
              <Input maxLength={50} />
            </Form.Item>
            <Form.Item
              name="phone"
              label={t('auth.phone')}
              rules={[
                {
                  validator: (_, value) =>
                    !value || /^[\d+\-()\s]{5,20}$/.test(value)
                      ? Promise.resolve()
                      : Promise.reject(new Error(t('account.phoneInvalid'))),
                },
              ]}
            >
              <Input maxLength={20} />
            </Form.Item>
            <Form.Item style={{ marginBottom: 0 }}>
              <Button type="primary" htmlType="submit" loading={basicLoading}>
                {t('account.save')}
              </Button>
            </Form.Item>
          </Form>
        </Card>

        <Card title={t('account.passwordTitle')}>
          <Form<PasswordFormValues>
            form={passwordForm}
            layout="vertical"
            onFinish={handlePasswordSubmit}
          >
            <Form.Item
              name="oldPassword"
              label={t('account.oldPassword')}
              rules={[{ required: true, message: t('account.oldPasswordRequired') }]}
            >
              <Input.Password autoComplete="current-password" />
            </Form.Item>
            <Form.Item
              name="newPassword"
              label={t('account.newPassword')}
              rules={[
                { required: true, message: t('account.passwordRequired') },
                {
                  validator: (_, value) =>
                    !value || PASSWORD_RULE.test(value)
                      ? Promise.resolve()
                      : Promise.reject(new Error(t('account.passwordRule'))),
                },
              ]}
            >
              <Input.Password autoComplete="new-password" />
            </Form.Item>
            <Form.Item
              name="confirmPassword"
              label={t('account.confirmPassword')}
              dependencies={['newPassword']}
              rules={[
                { required: true, message: t('account.passwordRequired') },
                ({ getFieldValue }) => ({
                  validator: (_, value) =>
                    !value || value === getFieldValue('newPassword')
                      ? Promise.resolve()
                      : Promise.reject(new Error(t('account.passwordMismatch'))),
                }),
              ]}
            >
              <Input.Password autoComplete="new-password" />
            </Form.Item>
            <Form.Item style={{ marginBottom: 0 }}>
              <Button type="primary" htmlType="submit" loading={passwordLoading}>
                {t('account.resetPassword')}
              </Button>
            </Form.Item>
          </Form>
        </Card>

        <Card title={t('account.oauthTitle')} loading={loading}>
          {providerList.length === 0 ? (
            <Text type="secondary">{t('account.oauthEmpty')}</Text>
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
              {providerList.map(({ provider, bound }) => (
                <div
                  key={provider.oAuthId}
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    gap: 12,
                    padding: '12px 16px',
                    border: '1px solid rgba(16, 24, 40, 0.08)',
                    borderRadius: 8,
                  }}
                >
                  {provider.iconUrl ? (
                    <img
                      src={resolveStorageUrl(provider.iconUrl)}
                      alt={provider.name ?? ''}
                      style={{ width: 28, height: 28, objectFit: 'contain' }}
                    />
                  ) : (
                    <Avatar size={28} icon={<LinkOutlined />} />
                  )}
                  <div style={{ flex: 1, minWidth: 0 }}>
                    <Text strong style={{ display: 'block' }}>
                      {provider.name ?? provider.provider}
                    </Text>
                    <Text type="secondary" style={{ fontSize: 12 }}>
                      {bound ? t('account.oauthBound') : t('account.oauthNotBound')}
                    </Text>
                  </div>
                  {bound ? (
                    <Popconfirm
                      title={t('account.unbindConfirm')}
                      onConfirm={() => provider.oAuthId && handleUnbind(provider.oAuthId)}
                    >
                      <Button danger>{t('account.unbind')}</Button>
                    </Popconfirm>
                  ) : (
                    <Button
                      type="primary"
                      icon={<LinkOutlined />}
                      onClick={() => handleBind(provider)}
                    >
                      {t('account.bind')}
                    </Button>
                  )}
                </div>
              ))}
            </div>
          )}
        </Card>
      </div>
    </Page>
  )
}

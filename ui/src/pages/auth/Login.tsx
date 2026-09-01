import { useEffect, useState } from 'react'
import { Link, useNavigate } from 'react-router'
import { Button, Card, Form, Input, Space, Tooltip, Typography } from 'antd'
import { LockOutlined, UserOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import { useFeedback } from '@/design-system'
import { getOAuthProviders, login, type OAuthProviderItem } from '@/api/auth'
import { resolveStorageUrl } from '@/utils/storage'

interface LoginFormValues {
  username: string
  password: string
}

export function Login() {
  const { t } = useTranslation()
  const feedback = useFeedback()
  const navigate = useNavigate()
  const [loading, setLoading] = useState(false)
  const [oauthLoading, setOauthLoading] = useState(true)
  const [providers, setProviders] = useState<OAuthProviderItem[]>([])

  useEffect(() => {
    let cancelled = false
    ;(async () => {
      try {
        const items = await getOAuthProviders()
        if (!cancelled) setProviders(items)
      } catch {
        // 错误已由全局请求中间件统一提示
      } finally {
        if (!cancelled) setOauthLoading(false)
      }
    })()
    return () => {
      cancelled = true
    }
  }, [])

  const handleFinish = async (values: LoginFormValues) => {
    setLoading(true)
    try {
      await login(values.username, values.password)
      feedback.success(t('auth.loginSuccess'))
      navigate('/dashboard', { replace: true })
    } catch (error) {
      // 错误已由全局请求中间件统一提示
      console.error('Login failed:', error)
    } finally {
      setLoading(false)
    }
  }

  return (
    <div
      style={{
        minHeight: '100vh',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
      }}
    >
      <Card style={{ width: 400 }}>
        <Typography.Title level={3} style={{ textAlign: 'center', marginBottom: 24 }}>
          {t('auth.loginTitle')}
        </Typography.Title>
        <Form<LoginFormValues> layout="vertical" onFinish={handleFinish} autoComplete="off">
          <Form.Item
            name="username"
            label={t('auth.username')}
            rules={[{ required: true, message: t('auth.usernamePlaceholder') }]}
          >
            <Input prefix={<UserOutlined />} placeholder={t('auth.usernamePlaceholder')} size="large" />
          </Form.Item>
          <Form.Item
            name="password"
            label={t('auth.password')}
            rules={[{ required: true, message: t('auth.passwordPlaceholder') }]}
          >
            <Input.Password prefix={<LockOutlined />} placeholder={t('auth.passwordPlaceholder')} size="large" />
          </Form.Item>
          <Form.Item style={{ marginBottom: 12 }}>
            <Button type="primary" htmlType="submit" block size="large" loading={loading}>
              {t('auth.login')}
            </Button>
          </Form.Item>
        </Form>
        {providers.length > 0 && (
          <>
            <div
              style={{
                display: 'flex',
                alignItems: 'center',
                marginBottom: 16,
                color: 'rgba(128,128,128,0.6)',
              }}
            >
              <div style={{ flex: 1, height: 1, background: 'rgba(128,128,128,0.25)' }} />
              <Typography.Text type="secondary" style={{ marginInline: 12, fontSize: 12 }}>
                {t('auth.oauthLogin')}
              </Typography.Text>
              <div style={{ flex: 1, height: 1, background: 'rgba(128,128,128,0.25)' }} />
            </div>
            <Space size="large" style={{ justifyContent: 'center', width: '100%' }}>
              {providers.map((item) => (
                <Tooltip key={item.oAuthId} title={item.name} placement="top">
                  <Button
                    type="text"
                    shape="circle"
                    size="large"
                    loading={oauthLoading}
                    onClick={() => {
                      if (item.redirectUrl) window.location.href = item.redirectUrl
                    }}
                  >
                    {item.iconUrl ? (
                      <img
                        src={resolveStorageUrl(item.iconUrl)}
                        alt={item.name ?? ''}
                        style={{ width: 28, height: 28 }}
                      />
                    ) : (
                      <span style={{ fontSize: 14 }}>{item.name}</span>
                    )}
                  </Button>
                </Tooltip>
              ))}
            </Space>
          </>
        )}
        <Typography.Text type="secondary">
          {t('auth.noAccount')} <Link to="/register">{t('auth.goRegister')}</Link>
        </Typography.Text>
      </Card>
    </div>
  )
}

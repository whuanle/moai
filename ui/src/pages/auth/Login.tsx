import { useEffect, useState } from 'react'
import { Link, useNavigate } from 'react-router'
import { Button, Card, Form, Input, Space, Tooltip, Typography, theme as antdTheme } from 'antd'
import { LockOutlined, UserOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import { useFeedback } from '@/design-system'
import { radius, spacing } from '@/design-system/theme'
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
  const { token } = antdTheme.useToken()
  const [loading, setLoading] = useState(false)
  const [providers, setProviders] = useState<OAuthProviderItem[]>([])

  useEffect(() => {
    let cancelled = false
    ;(async () => {
      try {
        const items = await getOAuthProviders()
        if (!cancelled) setProviders(items)
      } catch {
        // 错误已由全局请求中间件统一提示
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
        padding: spacing.lg,
        background: `linear-gradient(180deg, ${token.colorBgLayout} 0%, ${token.colorPrimaryBg} 100%)`,
      }}
    >
      <Card
        style={{
          width: 400,
          borderRadius: radius.lg,
          boxShadow: token.boxShadowTertiary,
        }}
      >
        <div style={{ textAlign: 'center', marginBottom: spacing.lg }}>
          <img src="/logo.svg" width={48} height={48} alt="MoAI" style={{ marginBottom: spacing.sm }} />
          <Typography.Title level={3} style={{ marginBottom: 4 }}>
            {t('auth.loginTitle')}
          </Typography.Title>
          <Typography.Text type="secondary">{t('auth.loginSubtitle')}</Typography.Text>
        </div>
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
              }}
            >
              <div style={{ flex: 1, height: 1, background: token.colorBorderSecondary }} />
              <Typography.Text type="secondary" style={{ marginInline: 12, fontSize: 12 }}>
                {t('auth.oauthLogin')}
              </Typography.Text>
              <div style={{ flex: 1, height: 1, background: token.colorBorderSecondary }} />
            </div>
            <Space size="large" style={{ justifyContent: 'center', width: '100%', marginBottom: spacing.md }}>
              {providers.map((item) => (
                <Tooltip key={item.oAuthId} title={item.name} placement="top">
                  <Button
                    type="text"
                    shape="circle"
                    size="large"
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
        <Typography.Text type="secondary" style={{ display: 'block', textAlign: 'center' }}>
          {t('auth.noAccount')} <Link to="/register">{t('auth.goRegister')}</Link>
        </Typography.Text>
      </Card>
    </div>
  )
}

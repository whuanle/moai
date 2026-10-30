import { useEffect, useState } from 'react'
import { Link, useNavigate } from 'react-router'
import { Button, Card, Form, Input, Space, Typography, theme as antdTheme } from 'antd'
import { IdcardOutlined, LockOutlined, MailOutlined, PhoneOutlined, SafetyCertificateOutlined, UserOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import { useFeedback } from '@/design-system'
import { radius, spacing } from '@/design-system/theme'
import { getServerInfo, register, sendEmailCode } from '@/api/auth'
import { AppLogo } from '@/layouts/components/AppLogo'
import { useSystemName } from '@/layouts/useSystemLogo'

interface RegisterFormValues {
  userName: string
  nickName?: string
  email: string
  phone?: string
  password: string
  confirmPassword: string
  emailCode?: string
}

export function Register() {
  const { t } = useTranslation()
  const feedback = useFeedback()
  const navigate = useNavigate()
  const { token } = antdTheme.useToken()
  const systemName = useSystemName()
  const [form] = Form.useForm<RegisterFormValues>()
  const [loading, setLoading] = useState(false)
  const [needEmailCode, setNeedEmailCode] = useState(false)
  const [sendingCode, setSendingCode] = useState(false)
  const [countdown, setCountdown] = useState(0)

  // 注册页在 AppLayout 之外，浏览器标签页标题需要自行同步网站名称
  useEffect(() => {
    document.title = systemName
  }, [systemName])

  // 注册验证码开关来自服务器信息（SMTP 启用 + 管理员开启注册验证码）
  useEffect(() => {
    let cancelled = false
    ;(async () => {
      try {
        const info = await getServerInfo()
        if (!cancelled) setNeedEmailCode(info.registerEmailCodeRequired)
      } catch {
        // 错误已由全局请求中间件统一提示
      }
    })()
    return () => {
      cancelled = true
    }
  }, [])

  // 发送冷却倒计时（秒）
  useEffect(() => {
    if (countdown <= 0) return undefined
    const timer = setTimeout(() => setCountdown((v) => v - 1), 1000)
    return () => clearTimeout(timer)
  }, [countdown])

  const handleSendCode = async () => {
    try {
      await form.validateFields(['email'])
    } catch {
      return
    }
    const email = (form.getFieldValue('email') as string | undefined)?.trim()
    if (!email) return
    setSendingCode(true)
    try {
      await sendEmailCode(email, 'register')
      feedback.success(t('auth.emailCodeSent'))
      setCountdown(60)
    } catch (error) {
      // 错误已由全局请求中间件统一提示
      console.error('Send email code failed:', error)
    } finally {
      setSendingCode(false)
    }
  }

  const handleFinish = async (values: RegisterFormValues) => {
    setLoading(true)
    try {
      await register({
        userName: values.userName,
        email: values.email,
        nickName: values.nickName,
        phone: values.phone,
        password: values.password,
        emailCode: values.emailCode,
      })
      feedback.success(t('auth.registerSuccess'))
      navigate('/login', { replace: true })
    } catch (error) {
      // 错误已由全局请求中间件统一提示
      console.error('Register failed:', error)
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
          width: 420,
          borderRadius: radius.lg,
          boxShadow: token.boxShadowTertiary,
        }}
      >
        <div style={{ textAlign: 'center', marginBottom: spacing.lg }}>
          <AppLogo size={48} alt={systemName} style={{ marginBottom: spacing.sm }} />
          <Typography.Title level={3} style={{ marginBottom: 4 }}>
            {systemName}
          </Typography.Title>
          <Typography.Text type="secondary">{t('auth.registerSubtitle')}</Typography.Text>
        </div>
        <Form<RegisterFormValues> form={form} layout="vertical" onFinish={handleFinish} autoComplete="off">
          <Form.Item
            name="userName"
            label={t('auth.username')}
            rules={[{ required: true, message: t('auth.usernamePlaceholder') }]}
          >
            <Input prefix={<UserOutlined />} placeholder={t('auth.usernamePlaceholder')} size="large" />
          </Form.Item>
          <Form.Item
            name="nickName"
            label={t('auth.nickName')}
            rules={[
              { required: true, message: t('auth.nickNamePlaceholder') },
              { min: 3, max: 20, message: t('auth.nickNamePlaceholder') },
            ]}
          >
            <Input prefix={<IdcardOutlined />} placeholder={t('auth.nickNamePlaceholder')} size="large" />
          </Form.Item>
          <Form.Item
            name="email"
            label={t('auth.email')}
            rules={[{ required: true, type: 'email', message: t('auth.emailPlaceholder') }]}
          >
            <Input prefix={<MailOutlined />} placeholder={t('auth.emailPlaceholder')} size="large" />
          </Form.Item>
          {needEmailCode && (
            <Form.Item label={t('auth.emailCode')} required>
              <Space.Compact style={{ width: '100%' }}>
                <Form.Item
                  name="emailCode"
                  noStyle
                  rules={[{ required: true, message: t('auth.emailCodeRequired') }]}
                >
                  <Input
                    prefix={<SafetyCertificateOutlined />}
                    placeholder={t('auth.emailCodePlaceholder')}
                    size="large"
                    maxLength={6}
                  />
                </Form.Item>
                <Button
                  size="large"
                  loading={sendingCode}
                  disabled={countdown > 0}
                  onClick={handleSendCode}
                >
                  {countdown > 0 ? `${countdown}s` : t('auth.sendCode')}
                </Button>
              </Space.Compact>
            </Form.Item>
          )}
          <Form.Item
            name="phone"
            label={t('auth.phone')}
            rules={[
              { required: true, message: t('auth.phonePlaceholder') },
              { pattern: /^1[3-9]\d{9}$/, message: t('auth.phonePlaceholder') },
            ]}
          >
            <Input prefix={<PhoneOutlined />} placeholder={t('auth.phonePlaceholder')} size="large" maxLength={11} />
          </Form.Item>
          <Form.Item
            name="password"
            label={t('auth.password')}
            rules={[{ required: true, min: 6, message: t('auth.passwordPlaceholder') }]}
          >
            <Input.Password prefix={<LockOutlined />} placeholder={t('auth.passwordPlaceholder')} size="large" />
          </Form.Item>
          <Form.Item
            name="confirmPassword"
            label={t('auth.confirmPassword')}
            dependencies={['password']}
            rules={[
              { required: true, message: t('auth.confirmPasswordPlaceholder') },
              ({ getFieldValue }) => ({
                validator(_, value) {
                  if (!value || getFieldValue('password') === value) return Promise.resolve()
                  return Promise.reject(new Error(t('auth.passwordMismatch')))
                },
              }),
            ]}
          >
            <Input.Password
              prefix={<LockOutlined />}
              placeholder={t('auth.confirmPasswordPlaceholder')}
              size="large"
            />
          </Form.Item>
          <Form.Item style={{ marginBottom: 12 }}>
            <Button type="primary" htmlType="submit" block size="large" loading={loading}>
              {t('auth.register')}
            </Button>
          </Form.Item>
        </Form>
        <Typography.Text type="secondary" style={{ display: 'block', textAlign: 'center' }}>
          {t('auth.haveAccount')} <Link to="/login">{t('auth.goLogin')}</Link>
        </Typography.Text>
      </Card>
    </div>
  )
}

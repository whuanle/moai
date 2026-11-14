import { useCallback, useEffect, useState } from 'react'
import { Link, useNavigate } from 'react-router'
import { Alert, Button, Card, Form, Input, Space, Typography, theme as antdTheme } from 'antd'
import { LockOutlined, MailOutlined, SafetyCertificateOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import { useFeedback } from '@/design-system'
import { radius, spacing } from '@/design-system/theme'
import { fetchChallengePolicy, getServerInfo, resetPasswordByEmail, sendEmailCode, type ChallengePolicy } from '@/api/auth'
import { AppLogo } from '@/layouts/components/AppLogo'
import { useSystemName } from '@/layouts/useSystemLogo'
import { CaptchaField } from './components/CaptchaField'
import { useCaptcha } from './components/useCaptcha'

interface ForgotPasswordFormValues {
  email: string
  code: string
  newPassword: string
  confirmPassword: string
  captchaCode?: string
}

export function ForgotPassword() {
  const { t } = useTranslation()
  const feedback = useFeedback()
  const navigate = useNavigate()
  const { token } = antdTheme.useToken()
  const systemName = useSystemName()
  const [form] = Form.useForm<ForgotPasswordFormValues>()
  const [loading, setLoading] = useState(false)
  const [resetEnabled, setResetEnabled] = useState<boolean | null>(null)
  const [policy, setPolicy] = useState<ChallengePolicy>({ captchaRequired: false, emailCodeRequired: false })
  const [sendingCode, setSendingCode] = useState(false)
  const [countdown, setCountdown] = useState(0)
  const { captcha, refreshCaptcha } = useCaptcha()

  // 找回密码页在 AppLayout 之外，浏览器标签页标题需要自行同步网站名称
  useEffect(() => {
    document.title = systemName
  }, [systemName])

  const refreshPolicy = useCallback(async () => {
    try {
      setPolicy(await fetchChallengePolicy('forgot'))
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }, [])

  // 找回密码可用性来自服务器信息（SMTP 邮件服务是否启用）；图形验证码策略来自 challenge_policy
  useEffect(() => {
    let cancelled = false
    ;(async () => {
      try {
        const info = await getServerInfo()
        if (!cancelled) setResetEnabled(info.passwordResetEnabled)
      } catch {
        // 错误已由全局请求中间件统一提示
        if (!cancelled) setResetEnabled(false)
      }
      await refreshPolicy()
    })()
    return () => {
      cancelled = true
    }
  }, [refreshPolicy])

  // 图形验证码按需拉取：策略命中且还没有可用图片时
  useEffect(() => {
    if (policy.captchaRequired && !captcha) void refreshCaptcha()
  }, [policy.captchaRequired, captcha, refreshCaptcha])

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
      await sendEmailCode(email, 'resetPassword', {
        captchaId: policy.captchaRequired ? captcha?.captchaId : undefined,
        captchaCode: (form.getFieldValue('captchaCode') as string | undefined) || undefined,
      })
      feedback.success(t('auth.emailCodeSent'))
      setCountdown(60)
      // 发码后重新拉取策略（after_fail 模式：本 IP 已发过码，下次发码需要过图形验证码）
      await refreshPolicy()
      void refreshCaptcha()
    } catch (error) {
      // 图形验证码错误时旧图已失效，换一张
      if (policy.captchaRequired) void refreshCaptcha()
      console.error('Send email code failed:', error)
    } finally {
      setSendingCode(false)
    }
  }

  const handleFinish = async (values: ForgotPasswordFormValues) => {
    setLoading(true)
    try {
      await resetPasswordByEmail(values.email.trim(), values.code, values.newPassword)
      feedback.success(t('auth.forgot.resetSuccess'))
      navigate('/login', { replace: true })
    } catch (error) {
      // 错误已由全局请求中间件统一提示
      console.error('Reset password failed:', error)
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
            {t('auth.forgot.title')}
          </Typography.Title>
          <Typography.Text type="secondary">{t('auth.forgot.subtitle')}</Typography.Text>
        </div>
        {resetEnabled === false && (
          <Alert
            type="warning"
            showIcon
            message={t('auth.forgot.disabled')}
            style={{ marginBottom: spacing.md }}
          />
        )}
        <Form<ForgotPasswordFormValues>
          form={form}
          layout="vertical"
          onFinish={handleFinish}
          autoComplete="off"
          disabled={resetEnabled !== true}
        >
          <Form.Item
            name="email"
            label={t('auth.email')}
            rules={[{ required: true, type: 'email', message: t('auth.emailPlaceholder') }]}
          >
            <Input prefix={<MailOutlined />} placeholder={t('auth.emailPlaceholder')} size="large" />
          </Form.Item>
          {policy.captchaRequired && (
            <Form.Item
              name="captchaCode"
              label={t('auth.captcha.label')}
              rules={[{ required: true, message: t('auth.captcha.required') }]}
            >
              <CaptchaField captcha={captcha} onRefresh={() => void refreshCaptcha()} />
            </Form.Item>
          )}
          <Form.Item label={t('auth.emailCode')} required>
            <Space.Compact style={{ width: '100%' }}>
              <Form.Item
                name="code"
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
                disabled={resetEnabled !== true || countdown > 0}
                onClick={handleSendCode}
              >
                {countdown > 0 ? `${countdown}s` : t('auth.sendCode')}
              </Button>
            </Space.Compact>
          </Form.Item>
          <Form.Item
            name="newPassword"
            label={t('auth.forgot.newPassword')}
            rules={[{ required: true, message: t('auth.forgot.passwordRequired') }]}
          >
            <Input.Password prefix={<LockOutlined />} placeholder={t('auth.forgot.passwordPlaceholder')} size="large" />
          </Form.Item>
          <Form.Item
            name="confirmPassword"
            label={t('auth.forgot.confirmPassword')}
            dependencies={['newPassword']}
            rules={[
              { required: true, message: t('auth.forgot.confirmPasswordPlaceholder') },
              ({ getFieldValue }) => ({
                validator(_, value) {
                  if (!value || getFieldValue('newPassword') === value) return Promise.resolve()
                  return Promise.reject(new Error(t('auth.passwordMismatch')))
                },
              }),
            ]}
          >
            <Input.Password
              prefix={<LockOutlined />}
              placeholder={t('auth.forgot.confirmPasswordPlaceholder')}
              size="large"
            />
          </Form.Item>
          <Form.Item style={{ marginBottom: 12 }}>
            <Button
              type="primary"
              htmlType="submit"
              block
              size="large"
              loading={loading}
              disabled={resetEnabled !== true}
            >
              {t('auth.forgot.reset')}
            </Button>
          </Form.Item>
        </Form>
        <Typography.Text type="secondary" style={{ display: 'block', textAlign: 'center' }}>
          <Link to="/login">{t('auth.forgot.backToLogin')}</Link>
        </Typography.Text>
      </Card>
    </div>
  )
}

import { useState } from 'react'
import { Link, useNavigate } from 'react-router'
import { App, Button, Card, Form, Input, Typography } from 'antd'
import { LockOutlined, UserOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import { login } from '@/api/auth'

interface LoginFormValues {
  username: string
  password: string
}

export function Login() {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const navigate = useNavigate()
  const [loading, setLoading] = useState(false)

  const handleFinish = async (values: LoginFormValues) => {
    setLoading(true)
    try {
      await login(values.username, values.password)
      message.success(t('auth.loginSuccess'))
      navigate('/dashboard', { replace: true })
    } catch (error) {
      console.error('Login failed:', error)
      const detail = (error as { detail?: string }).detail
      message.error(detail ?? '登录失败')
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
        <Typography.Text type="secondary">
          {t('auth.noAccount')} <Link to="/register">{t('auth.goRegister')}</Link>
        </Typography.Text>
      </Card>
    </div>
  )
}

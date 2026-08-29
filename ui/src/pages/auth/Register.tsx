import { useState } from 'react'
import { Link, useNavigate } from 'react-router'
import { App, Button, Card, Form, Input, Typography } from 'antd'
import { IdcardOutlined, LockOutlined, MailOutlined, PhoneOutlined, UserOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import { register } from '@/api/auth'

interface RegisterFormValues {
  userName: string
  nickName?: string
  email: string
  phone?: string
  password: string
  confirmPassword: string
}

export function Register() {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const navigate = useNavigate()
  const [loading, setLoading] = useState(false)

  const handleFinish = async (values: RegisterFormValues) => {
    setLoading(true)
    try {
      await register({
        userName: values.userName,
        email: values.email,
        nickName: values.nickName,
        phone: values.phone,
        password: values.password,
      })
      message.success(t('auth.registerSuccess'))
      navigate('/login', { replace: true })
    } catch (error) {
      console.error('Register failed:', error)
      const detail = (error as { detail?: string }).detail
      message.error(detail ?? '注册失败')
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
        padding: 16,
      }}
    >
      <Card style={{ width: 420 }}>
        <Typography.Title level={3} style={{ textAlign: 'center', marginBottom: 24 }}>
          {t('auth.registerTitle')}
        </Typography.Title>
        <Form<RegisterFormValues> layout="vertical" onFinish={handleFinish} autoComplete="off">
          <Form.Item
            name="userName"
            label={t('auth.username')}
            rules={[{ required: true, message: t('auth.usernamePlaceholder') }]}
          >
            <Input prefix={<UserOutlined />} placeholder={t('auth.usernamePlaceholder')} size="large" />
          </Form.Item>
          <Form.Item name="nickName" label={t('auth.nickName')}>
            <Input prefix={<IdcardOutlined />} placeholder={t('auth.nickNamePlaceholder')} size="large" />
          </Form.Item>
          <Form.Item
            name="email"
            label={t('auth.email')}
            rules={[{ required: true, type: 'email', message: t('auth.emailPlaceholder') }]}
          >
            <Input prefix={<MailOutlined />} placeholder={t('auth.emailPlaceholder')} size="large" />
          </Form.Item>
          <Form.Item name="phone" label={t('auth.phone')}>
            <Input prefix={<PhoneOutlined />} placeholder={t('auth.phonePlaceholder')} size="large" />
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
                  return Promise.reject(new Error(t('auth.confirmPasswordPlaceholder')))
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
        <Typography.Text type="secondary">
          {t('auth.haveAccount')} <Link to="/login">{t('auth.goLogin')}</Link>
        </Typography.Text>
      </Card>
    </div>
  )
}

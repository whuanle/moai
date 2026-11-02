import { useState } from 'react'
import { Button, Form, Input } from 'antd'
import { useTranslation } from 'react-i18next'
import { Card, Page, feedback } from '@/design-system'
import { resetPassword } from '@/api/account'

interface PasswordFormValues {
  oldPassword: string
  newPassword: string
  confirmPassword: string
}

const PASSWORD_RULE = /^(?=.*[A-Za-z])(?=.*\d)[A-Za-z\d\S]{8,20}$/

/** 修改密码：自助改密（旧密码 + 新密码两次确认） */
export function AccountPassword() {
  const { t } = useTranslation()
  const [passwordForm] = Form.useForm<PasswordFormValues>()
  const [passwordLoading, setPasswordLoading] = useState(false)

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

  return (
    <Page>
      <div style={{ maxWidth: 720, display: 'flex', flexDirection: 'column', gap: 24 }}>
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
      </div>
    </Page>
  )
}

import { Button, ConfigProvider, Form, Space, Typography } from 'antd'
import type { FormProps } from 'antd'
import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { spacing } from '@/design-system/theme'

export interface FormPageProps extends Omit<FormProps, 'onFinish' | 'title' | 'children'> {
  title?: ReactNode
  children?: ReactNode
  onFinish: (values: Record<string, unknown>) => void | Promise<void>
  onCancel?: () => void
  submitting?: boolean
  footerExtra?: ReactNode
  actionTop?: ReactNode
}

export function FormPage({
  title,
  onFinish,
  onCancel,
  submitting,
  footerExtra,
  actionTop,
  children,
  ...rest
}: FormPageProps) {
  const { t } = useTranslation()
  return (
    <div style={{ width: '100%', maxWidth: 720, margin: '0 auto' }}>
      {title && (
        <Typography.Title level={4} style={{ marginTop: 0, marginBottom: spacing.lg }}>
          {title}
        </Typography.Title>
      )}
      {actionTop}
      <ConfigProvider button={{ autoInsertSpace: false }}>
        <Form layout="vertical" onFinish={onFinish as FormProps['onFinish']} {...rest}>
          {children}
          <Form.Item style={{ marginTop: spacing.lg, marginBottom: 0 }}>
            <Space>
              <Button type="primary" htmlType="submit" loading={submitting}>
                {t('ds.form.submit')}
              </Button>
              {onCancel && <Button onClick={onCancel}>{t('ds.form.cancel')}</Button>}
              {footerExtra}
            </Space>
          </Form.Item>
        </Form>
      </ConfigProvider>
    </div>
  )
}

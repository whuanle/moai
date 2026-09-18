import { Button, ConfigProvider, Form, Space } from 'antd'
import type { FormInstance } from 'antd'
import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { spacing } from '@/design-system/theme'

export interface QueryBarProps {
  form?: FormInstance
  onSearch?: (values: Record<string, unknown>) => void
  onReset?: () => void
  loading?: boolean
  children?: ReactNode
  /** 追加在查询/重置按钮右侧的操作区（如新建、刷新） */
  extra?: ReactNode
}

export function QueryBar({ form, onSearch, onReset, loading, children, extra }: QueryBarProps) {
  const { t } = useTranslation()
  const [internalForm] = Form.useForm()
  const activeForm = form ?? internalForm

  return (
    <ConfigProvider button={{ autoInsertSpace: false }}>
      <Form
        form={activeForm}
        layout="inline"
        style={{ marginBottom: spacing.lg }}
        onFinish={(values) => onSearch?.(values)}
      >
        {children}
        <Form.Item>
          <Space>
            <Button
              type="primary"
              htmlType="submit"
              loading={loading}
              onClick={(e) => {
                e.preventDefault()
                onSearch?.(activeForm.getFieldsValue())
              }}
            >
              {t('ds.query.search')}
            </Button>
            <Button
              onClick={() => {
                activeForm.resetFields()
                onReset?.()
              }}
            >
              {t('ds.query.reset')}
            </Button>
            {extra}
          </Space>
        </Form.Item>
      </Form>
    </ConfigProvider>
  )
}

import { Button, ConfigProvider, Divider, Form, Space } from 'antd'
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
  /** 操作按钮组（如新建、刷新），紧跟在重置按钮右侧，以竖分隔线（|）与筛选按钮区隔 */
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
        style={{
          // 裸排在页面底色上（Dify 模式），与下方 DataTable 的容器底色卡片之间仅隔同色留白
          marginBottom: spacing.md,
        }}
        onFinish={(values) => onSearch?.(values)}
      >
        {children}
        <Form.Item>
          <div style={{ display: 'flex', alignItems: 'center' }}>
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
            </Space>
            {extra ? (
              <>
                <Divider type="vertical" style={{ marginInline: spacing.sm }} />
                {extra}
              </>
            ) : null}
          </div>
        </Form.Item>
      </Form>
    </ConfigProvider>
  )
}

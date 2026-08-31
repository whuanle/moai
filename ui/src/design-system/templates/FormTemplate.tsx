import { App, Form, Input, Select, InputNumber } from 'antd'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Page, FormPage } from '@/design-system'

export function FormTemplate() {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const [submitting, setSubmitting] = useState(false)

  return (
    <Page>
      <FormPage
        title={t('ds.form.title')}
        submitting={submitting}
        onCancel={() => {}}
        onFinish={() => {
          setSubmitting(true)
          setTimeout(() => {
            setSubmitting(false)
            message.success(t('ds.form.success'))
          }, 500)
        }}
      >
        <Form.Item name="name" label={t('ds.form.nameLabel')} rules={[{ required: true }]}>
          <Input placeholder={t('ds.form.namePlaceholder')} />
        </Form.Item>
        <Form.Item name="type" label={t('ds.form.type')} rules={[{ required: true }]}>
          <Select
            options={[
              { value: 'single', label: t('ds.form.typeSingle') },
              { value: 'group', label: t('ds.form.typeGroup') },
            ]}
          />
        </Form.Item>
        <Form.Item name="count" label={t('ds.form.count')}>
          <InputNumber style={{ width: '100%' }} />
        </Form.Item>
      </FormPage>
    </Page>
  )
}

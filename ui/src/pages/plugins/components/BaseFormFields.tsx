import { Col, Form, Input, Row, Select, Switch } from 'antd'
import { useTranslation } from 'react-i18next'
import type { PluginClassify } from '@/api/classify'

const { TextArea } = Input

interface BaseFormFieldsProps {
  classifies: PluginClassify[]
  showServerUrl?: boolean
  serverUrlLabel?: string
  showIsPublic?: boolean
}

/** MCP/OpenAPI 共用的基础表单字段（名称/标题/地址/分类/描述/公开）. */
export function BaseFormFields({
  classifies,
  showServerUrl = true,
  serverUrlLabel,
  showIsPublic = true,
}: BaseFormFieldsProps) {
  const { t } = useTranslation()
  return (
    <>
      <Row gutter={16}>
        <Col span={12}>
          <Form.Item
            name="name"
            label={t('plugins.formPluginName')}
            rules={[
              { required: true, message: t('plugins.pluginNameRequired') },
              { pattern: /^[a-zA-Z_]+$/, message: t('plugins.pluginNameRule') },
            ]}
            extra={t('plugins.formPluginNameExtra')}
          >
            <Input placeholder={t('plugins.formPluginNamePlaceholder')} maxLength={30} />
          </Form.Item>
        </Col>
        <Col span={12}>
          <Form.Item
            name="title"
            label={t('plugins.formPluginTitle')}
            rules={[{ required: true, message: t('plugins.pluginTitleRequired') }]}
            extra={t('plugins.formPluginTitleExtra')}
          >
            <Input placeholder={t('plugins.formPluginTitlePlaceholder')} maxLength={20} />
          </Form.Item>
        </Col>
      </Row>
      <Row gutter={16}>
        {showServerUrl && (
          <Col span={12}>
            <Form.Item
              name="serverUrl"
              label={serverUrlLabel ?? t('plugins.formServerUrl')}
              rules={[{ required: true, message: t('plugins.serverUrlRequired') }]}
            >
              <Input placeholder={t('plugins.formServerUrlPlaceholder')} />
            </Form.Item>
          </Col>
        )}
        <Col span={showServerUrl ? 12 : 24}>
          <Form.Item name="classifyId" label={t('plugins.formClassify')}>
            <Select placeholder={t('plugins.formClassifyPlaceholder')} allowClear>
              {classifies.map((item) => (
                <Select.Option key={item.classifyId} value={item.classifyId}>
                  {item.name}
                </Select.Option>
              ))}
            </Select>
          </Form.Item>
        </Col>
      </Row>
      <Form.Item name="description" label={t('plugins.formDescription')}>
        <TextArea placeholder={t('plugins.formDescriptionPlaceholder')} rows={3} maxLength={255} />
      </Form.Item>
      {showIsPublic && (
        <Form.Item name="isPublic" label={t('plugins.formIsPublic')} valuePropName="checked" initialValue={false}>
          <Switch checkedChildren={t('plugins.isPublic')} unCheckedChildren={t('plugins.notPublic')} />
        </Form.Item>
      )}
    </>
  )
}

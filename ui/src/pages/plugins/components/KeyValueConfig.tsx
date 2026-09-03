import { Button, Col, Form, Input, Row } from 'antd'
import { MinusCircleOutlined, PlusOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'

interface KeyValueConfigProps {
  /** Form.List 字段名（header / query）. */
  name: string
  /** 标题（Header / Query）. */
  title: string
}

/** 可增删的键值对配置，用于 Header/Query 编辑. */
export function KeyValueConfig({ name, title }: KeyValueConfigProps) {
  const { t } = useTranslation()
  return (
    <Form.List name={name}>
      {(fields, { add, remove }) => (
        <>
          {fields.map(({ key, name: fieldName, ...restField }) => (
            <Row gutter={8} key={key} align="middle" style={{ marginBottom: 8 }}>
              <Col span={10}>
                <Form.Item
                  {...restField}
                  name={[fieldName, 'key']}
                  rules={[{ required: true, message: t('plugins.keyRequired') }]}
                >
                  <Input placeholder={title === 'Header' ? t('plugins.headerKey') : t('plugins.queryKey')} />
                </Form.Item>
              </Col>
              <Col span={10}>
                <Form.Item
                  {...restField}
                  name={[fieldName, 'value']}
                  rules={[{ required: true, message: t('plugins.valueRequired') }]}
                >
                  <Input placeholder={title === 'Header' ? t('plugins.headerValue') : t('plugins.queryValue')} />
                </Form.Item>
              </Col>
              <Col span={4}>
                <Button type="text" danger icon={<MinusCircleOutlined />} onClick={() => remove(fieldName)} />
              </Col>
            </Row>
          ))}
          <Form.Item>
            <Button type="dashed" block icon={<PlusOutlined />} onClick={() => add()}>
              {title === 'Header' ? t('plugins.addHeader') : t('plugins.addQuery')}
            </Button>
          </Form.Item>
        </>
      )}
    </Form.List>
  )
}

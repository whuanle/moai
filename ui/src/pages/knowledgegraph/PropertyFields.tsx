import { useTranslation } from 'react-i18next'
import { Form, Input, InputNumber, Select, Switch, DatePicker, Space, Button } from 'antd'
import { DeleteOutlined, PlusOutlined } from '@ant-design/icons'
import dayjs from 'dayjs'
import type { KnowledgeGraphEntityTypeProperty } from '@/api/knowledgeGraph'

/** 属性定义（name 必填的规范化形态） */
export interface PropertyDef {
  name: string
  type: string
  required?: boolean
  description?: string
}

/** api 属性定义 → 规范化 PropertyDef */
export function toPropertyDefs(list?: KnowledgeGraphEntityTypeProperty[] | null): PropertyDef[] {
  return (list ?? [])
    .filter((x) => !!x.name)
    .map((x) => ({ name: x.name ?? '', type: x.type ?? 'string', required: x.required ?? false, description: x.description ?? '' }))
}

export const PROPERTY_TYPE_OPTIONS = [
  { value: 'string', labelKey: 'knowledgegraph.props.typeString' },
  { value: 'number', labelKey: 'knowledgegraph.props.typeNumber' },
  { value: 'boolean', labelKey: 'knowledgegraph.props.typeBoolean' },
  { value: 'date', labelKey: 'knowledgegraph.props.typeDate' },
]

/** 按实体类型的属性定义渲染实例属性输入项（Form.Item name 为 ['props', 属性名]） */
export function PropertyInputs({ properties, disabled = false }: { properties: PropertyDef[]; disabled?: boolean }) {
  const { t } = useTranslation()
  return (
    <>
      {properties.map((prop) => (
        <Form.Item
          key={prop.name}
          name={['props', prop.name]}
          label={prop.description ? `${prop.name}（${prop.description}）` : prop.name}
          valuePropName={prop.type === 'boolean' ? 'checked' : 'value'}
          rules={prop.required ? [{ required: true, message: `${t('knowledgegraph.props.requiredPrefix')}${prop.name}` }] : []}
        >
          <PropertyValueInput type={prop.type} disabled={disabled} />
        </Form.Item>
      ))}
    </>
  )
}

function PropertyValueInput({ type, disabled }: { type: string; disabled?: boolean }) {
  if (type === 'number') return <InputNumber style={{ width: '100%' }} disabled={disabled} />
  if (type === 'boolean') return <Switch disabled={disabled} />
  if (type === 'date') return <DatePicker style={{ width: '100%' }} disabled={disabled} />
  return <Input maxLength={2000} disabled={disabled} />
}

/** 模型页属性定义编辑器（Form.List name 由调用方指定，默认 properties） */
export function PropertyListEditor({ listName = 'properties' }: { listName?: string }) {
  const { t } = useTranslation()
  return (
    <Form.List name={listName}>
      {(fields, { add, remove }) => (
        <>
          {fields.map((field) => (
            <Space key={field.key} align="baseline" style={{ display: 'flex', marginBottom: 4 }} wrap>
              <Form.Item
                name={[field.name, 'name']}
                rules={[
                  { required: true, message: t('knowledgegraph.props.nameRequired') },
                  { max: 100, message: t('knowledgegraph.props.nameMax') },
                ]}
                style={{ marginBottom: 4 }}
              >
                <Input placeholder={t('knowledgegraph.props.namePlaceholder')} style={{ width: 140 }} maxLength={100} />
              </Form.Item>
              <Form.Item name={[field.name, 'type']} initialValue="string" style={{ marginBottom: 4 }}>
                <Select
                  style={{ width: 100 }}
                  options={PROPERTY_TYPE_OPTIONS.map((x) => ({ value: x.value, label: t(x.labelKey) }))}
                />
              </Form.Item>
              <Form.Item name={[field.name, 'required']} valuePropName="checked" style={{ marginBottom: 4 }}>
                <Switch checkedChildren={t('knowledgegraph.props.required')} unCheckedChildren={t('knowledgegraph.props.optional')} />
              </Form.Item>
              <Form.Item name={[field.name, 'description']} style={{ marginBottom: 4, flex: 1 }}>
                <Input placeholder={t('knowledgegraph.props.descPlaceholder')} style={{ width: 160 }} maxLength={255} />
              </Form.Item>
              <Button type="text" size="small" danger icon={<DeleteOutlined />} onClick={() => remove(field.name)} />
            </Space>
          ))}
          <Button type="dashed" size="small" icon={<PlusOutlined />} onClick={() => add({ name: '', type: 'string', required: false, description: '' })}>
            {t('knowledgegraph.props.addProperty')}
          </Button>
        </>
      )}
    </Form.List>
  )
}

/** 表单值 → 提交载荷：属性值序列化为字符串字典（boolean → 'true/false'，date → ISO） */
export function serializePropertyValues(
  values: Record<string, unknown> | undefined,
  defs: PropertyDef[],
): Record<string, string> {
  const result: Record<string, string> = {}
  if (!values) return result
  for (const def of defs) {
    const v = values[def.name]
    if (v === undefined || v === null || v === '') continue
    if (def.type === 'boolean') result[def.name] = v ? 'true' : 'false'
    else if (def.type === 'date') result[def.name] = dayjs(v as string | Date).toISOString()
    else result[def.name] = String(v)
  }
  return result
}

/** 回显：属性字符串值 → 表单初始值（number/boolean/date 反向转换） */
export function deserializePropertyValues(
  stored: Record<string, string> | undefined,
  defs: PropertyDef[],
): Record<string, unknown> {
  const result: Record<string, unknown> = {}
  if (!stored) return result
  for (const def of defs) {
    if (!(def.name in stored)) continue
    const v = stored[def.name]
    if (v == null) continue
    if (def.type === 'boolean') result[def.name] = v === 'true'
    else if (def.type === 'number') result[def.name] = Number(v)
    else if (def.type === 'date') result[def.name] = dayjs(v)
    else result[def.name] = v
  }
  return result
}


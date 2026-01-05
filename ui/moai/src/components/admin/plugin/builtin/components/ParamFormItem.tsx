/**
 * 表单参数项渲染组件
 * 根据不同的字段类型渲染对应的表单控件
 */
import { useCallback } from "react";
import {
  Form,
  Input,
  InputNumber,
  Switch,
  Space,
  Typography,
  Button,
  Tooltip,
} from "antd";
import { CodeOutlined } from "@ant-design/icons";
import type { FormInstance } from "antd";
import {
  NativePluginConfigFieldTemplate,
  PluginConfigFieldTypeObject,
} from "../../../../../apiClient/models";

interface ParamFormItemProps {
  param: NativePluginConfigFieldTemplate;
  formInstance?: FormInstance;
  onOpenCodeEditor?: (
    fieldKey: string,
    currentValue: string,
    formInstance: FormInstance
  ) => void;
}

export default function ParamFormItem({
  param,
  formInstance,
  onOpenCodeEditor,
}: ParamFormItemProps) {
  if (!param.key) return null;

  const fieldType = param.fieldType;
  const isRequired = param.isRequired === true;

  const label = (
    <Space>
      <Typography.Text>{param.key}</Typography.Text>
      {isRequired && <Typography.Text type="danger">*</Typography.Text>}
    </Space>
  );

  // 根据字段类型解析默认值
  let initialValue: any = undefined;
  if (param.exampleValue !== null && param.exampleValue !== undefined) {
    if (fieldType === PluginConfigFieldTypeObject.Boolean) {
      const valueStr = String(param.exampleValue).toLowerCase();
      initialValue = valueStr === "true" || valueStr === "1";
    } else if (
      fieldType === PluginConfigFieldTypeObject.Number ||
      fieldType === PluginConfigFieldTypeObject.Integer
    ) {
      initialValue = Number(param.exampleValue);
    } else {
      initialValue = param.exampleValue;
    }
  }

  const rules = isRequired
    ? [{ required: true, message: `请输入${param.key}` }]
    : [];

  // Code 类型
  if (fieldType === PluginConfigFieldTypeObject.Code) {
    return (
      <Form.Item
        key={param.key}
        name={param.key}
        label={label}
        help={param.description || undefined}
        initialValue={initialValue || "// 在这里写代码"}
        rules={rules}
        className="param-form-item"
      >
        <Form.Item noStyle shouldUpdate>
          {({ getFieldValue }) => {
            const fieldValue = getFieldValue(param.key);
            return (
              <div className="param-code-editor-wrapper">
                <Input.TextArea
                  rows={6}
                  placeholder="JavaScript 代码"
                  readOnly
                  value={fieldValue || initialValue || "// 在这里写代码"}
                  className="param-code-textarea"
                />
                <div className="param-code-actions">
                  <Tooltip title="打开代码编辑器">
                    <Button
                      type="link"
                      size="small"
                      icon={<CodeOutlined />}
                      onClick={() => {
                        if (!param.key || !formInstance || !onOpenCodeEditor)
                          return;
                        const currentValue = fieldValue || initialValue || "";
                        onOpenCodeEditor(param.key, currentValue, formInstance);
                      }}
                    >
                      打开代码编辑器
                    </Button>
                  </Tooltip>
                </div>
              </div>
            );
          }}
        </Form.Item>
      </Form.Item>
    );
  }

  // Boolean 类型
  if (fieldType === PluginConfigFieldTypeObject.Boolean) {
    return (
      <Form.Item
        key={param.key}
        name={param.key}
        label={label}
        help={param.description || undefined}
        valuePropName="checked"
        initialValue={initialValue}
        rules={rules}
        className="param-form-item"
      >
        <Switch />
      </Form.Item>
    );
  }

  // Number / Integer 类型
  if (
    fieldType === PluginConfigFieldTypeObject.Number ||
    fieldType === PluginConfigFieldTypeObject.Integer
  ) {
    return (
      <Form.Item
        key={param.key}
        name={param.key}
        label={label}
        help={param.description || undefined}
        initialValue={initialValue}
        rules={rules}
        className="param-form-item"
      >
        <InputNumber style={{ width: "100%" }} />
      </Form.Item>
    );
  }

  // Object / Map 类型
  if (
    fieldType === PluginConfigFieldTypeObject.Object ||
    fieldType === PluginConfigFieldTypeObject.Map
  ) {
    return (
      <Form.Item
        key={param.key}
        name={param.key}
        label={label}
        help={param.description || undefined}
        initialValue={initialValue}
        rules={rules}
        className="param-form-item"
      >
        <Input.TextArea rows={4} placeholder="请输入 JSON 格式" />
      </Form.Item>
    );
  }

  // 默认字符串类型
  return (
    <Form.Item
      key={param.key}
      name={param.key}
      label={label}
      help={param.description || undefined}
      initialValue={initialValue}
      rules={rules}
      className="param-form-item"
    >
      <Input.TextArea
        rows={3}
        placeholder={param.exampleValue ? `示例: ${param.exampleValue}` : ""}
      />
    </Form.Item>
  );
}

/**
 * MCP 插件导入/编辑模态框
 */
import { useCallback } from "react";
import { Modal, Form, Input, Switch, Row, Col, Select, Divider, Button, Spin } from "antd";
import { PlusOutlined, MinusCircleOutlined } from "@ant-design/icons";
import type { PluginClassifyItem, KeyValueString } from "../../../../../apiClient/models";

const { TextArea } = Input;

interface McpPluginModalProps {
  open: boolean;
  loading: boolean;
  isEdit?: boolean;
  form: ReturnType<typeof Form.useForm>[0];
  classifyList: PluginClassifyItem[];
  onOk: () => void;
  onCancel: () => void;
}

// 渲染 Key-Value 配置
const renderKeyValueConfig = (name: string, title: string) => (
  <>
    <Divider orientation="left" style={{ fontSize: 13, color: "var(--color-text-secondary)" }}>{title} 配置</Divider>
    <Form.List name={name}>
      {(fields, { add, remove }) => (
        <>
          {fields.map(({ key, name: fieldName, ...restField }) => (
            <Row gutter={16} key={key} style={{ marginBottom: 8 }}>
              <Col span={10}>
                <Form.Item {...restField} name={[fieldName, "key"]} rules={[{ required: true, message: `请输入${title} Key` }]}>
                  <Input placeholder={`${title} Key`} />
                </Form.Item>
              </Col>
              <Col span={10}>
                <Form.Item {...restField} name={[fieldName, "value"]} rules={[{ required: true, message: `请输入${title} Value` }]}>
                  <Input placeholder={`${title} Value`} />
                </Form.Item>
              </Col>
              <Col span={4}>
                <Button type="text" icon={<MinusCircleOutlined />} onClick={() => remove(fieldName)} danger />
              </Col>
            </Row>
          ))}
          <Form.Item>
            <Button type="dashed" onClick={() => add()} block icon={<PlusOutlined />}>添加 {title}</Button>
          </Form.Item>
        </>
      )}
    </Form.List>
  </>
);

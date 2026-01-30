/**
 * 开始节点配置组件
 * 允许配置工作流的启动参数（Map 类型）
 */

import { Form, Input, Button, Space, Select, Switch, message, Collapse } from 'antd';
import { PlusOutlined, DeleteOutlined } from '@ant-design/icons';
import { useState, useEffect } from 'react';
import { FieldType, FieldExpressionType } from '../types';
import type { ExtendedFieldDefine } from '../types';
import './NodeConfig.css';

interface StartNodeConfigProps {
  nodeId: string;
  config: {
    inputFields: ExtendedFieldDefine[];
    outputFields: ExtendedFieldDefine[];
    settings: Record<string, unknown>;
  };
  onSave: (config: {
    inputFields: ExtendedFieldDefine[];
    outputFields: ExtendedFieldDefine[];
    settings: Record<string, unknown>;
  }) => void;
  onCancel: () => void;
}

export function StartNodeConfig({ nodeId, config, onSave, onCancel }: StartNodeConfigProps) {
  const [form] = Form.useForm();
  // 开始节点的 parameters 字段的子字段
  const [mapFields, setMapFields] = useState<ExtendedFieldDefine[]>(
    config.inputFields?.[0]?.children || []
  );
  const [messageApi, contextHolder] = message.useMessage();

  useEffect(() => {
    // 初始化表单
    form.setFieldsValue({
      description: config.settings.description || '',
      autoStart: config.settings.autoStart || false,
    });
  }, [config, form]);

  // 添加 Map 字段
  const handleAddMapField = () => {
    const newField: ExtendedFieldDefine = {
      fieldName: `field_${mapFields.length + 1}`,
      fieldType: FieldType.String,
      expressionType: FieldExpressionType.Constant,
      value: '',
      isRequired: false,
      description: '',
    };
    setMapFields([...mapFields, newField]);
  };

  // 删除 Map 字段
  const handleDeleteMapField = (index: number) => {
    const newFields = mapFields.filter((_, i) => i !== index);
    setMapFields(newFields);
  };

  // 更新 Map 字段
  const handleUpdateMapField = (index: number, field: keyof ExtendedFieldDefine, value: any) => {
    const newFields = [...mapFields];
    newFields[index] = { ...newFields[index], [field]: value };
    setMapFields(newFields);
  };

  // 保存配置
  const handleSave = async () => {
    try {
      const values = await form.validateFields();
      
      // 验证字段名称唯一性
      const fieldNames = mapFields.map(f => f.fieldName);
      const uniqueNames = new Set(fieldNames);
      if (fieldNames.length !== uniqueNames.size) {
        messageApi.error('字段名称不能重复');
        return;
      }

      // 验证字段名称格式
      const invalidNames = mapFields.filter(f => f.fieldName && !/^[a-zA-Z_][a-zA-Z0-9_]*$/.test(f.fieldName));
      if (invalidNames.length > 0) {
        messageApi.error('字段名称只能包含字母、数字和下划线，且不能以数字开头');
        return;
      }

      // 构建配置 - parameters 字段包含子字段
      const parametersField: ExtendedFieldDefine = {
        fieldName: 'parameters',
        fieldType: FieldType.Map,
        isRequired: false,
        description: '启动参数（Map 类型）',
        children: mapFields,
      };

      const newConfig = {
        inputFields: [parametersField],
        outputFields: [parametersField], // 开始节点的输出字段与输入字段相同
        settings: {
          description: values.description,
          autoStart: values.autoStart,
        },
      };

      onSave(newConfig);
      messageApi.success('配置已保存');
    } catch (error) {
      console.error('保存配置失败:', error);
      messageApi.error('请检查表单填写是否正确');
    }
  };

  // 渲染字段值输入框（根据字段来源类型）
  const renderValueInput = (field: ExtendedFieldDefine, index: number) => {
    const expressionType = field.expressionType || FieldExpressionType.Constant;

    switch (expressionType) {
      case FieldExpressionType.Constant:
        // 常量值 - 根据字段类型显示不同的输入框
        if (field.fieldType === FieldType.Boolean) {
          return (
            <Switch
              checked={field.value as boolean}
              onChange={checked => handleUpdateMapField(index, 'value', checked)}
            />
          );
        } else if (field.fieldType === FieldType.Number) {
          return (
            <Input
              type="number"
              placeholder="输入数值"
              value={field.value}
              onChange={e => handleUpdateMapField(index, 'value', e.target.value)}
            />
          );
        } else {
          return (
            <Input
              placeholder="输入常量值"
              value={field.value}
              onChange={e => handleUpdateMapField(index, 'value', e.target.value)}
            />
          );
        }
      
      case FieldExpressionType.Variable:
        return (
          <Input
            placeholder="输入变量名，如：context.userId"
            value={field.value}
            onChange={e => handleUpdateMapField(index, 'value', e.target.value)}
          />
        );
      
      case FieldExpressionType.Expression:
        return (
          <Input.TextArea
            placeholder="输入表达式，如：context.price * 0.9"
            value={field.value}
            onChange={e => handleUpdateMapField(index, 'value', e.target.value)}
            rows={2}
          />
        );
      
      case FieldExpressionType.NodeOutput:
        return (
          <Input
            placeholder="输入节点输出路径，如：node_1.output.result"
            value={field.value}
            onChange={e => handleUpdateMapField(index, 'value', e.target.value)}
          />
        );
      
      case FieldExpressionType.Context:
        return (
          <Input
            placeholder="输入上下文路径，如：workflow.startTime"
            value={field.value}
            onChange={e => handleUpdateMapField(index, 'value', e.target.value)}
          />
        );
      
      default:
        return (
          <Input
            placeholder="输入值"
            value={field.value}
            onChange={e => handleUpdateMapField(index, 'value', e.target.value)}
          />
        );
    }
  };

  return (
    <div className="node-config-panel">
      {contextHolder}
      <div className="node-config-header">
        <h3>开始节点配置</h3>
        <p className="node-config-desc">配置工作流的启动参数（Map 类型），这些参数将在工作流启动时传入</p>
      </div>

      <Form form={form} layout="vertical" className="node-config-form">
        <Form.Item
          label="节点描述"
          name="description"
          tooltip="描述此开始节点的用途"
        >
          <Input.TextArea
            rows={2}
            placeholder="例如：接收用户输入的查询参数"
            maxLength={200}
            showCount
          />
        </Form.Item>

        <Form.Item
          label="自动启动"
          name="autoStart"
          valuePropName="checked"
          tooltip="是否在工作流部署后自动启动"
        >
          <Switch />
        </Form.Item>

        <div className="node-config-section">
          <div className="section-header">
            <h4>Parameters 字段配置 (Map)</h4>
            <Button
              type="dashed"
              icon={<PlusOutlined />}
              onClick={handleAddMapField}
              size="small"
            >
              添加字段
            </Button>
          </div>

          <div className="parameters-list">
            {mapFields.length === 0 ? (
              <div className="empty-parameters">
                <p>暂无字段，点击"添加字段"按钮添加 Map 中的字段</p>
              </div>
            ) : (
              mapFields.map((field, index) => (
                <Collapse
                  key={index}
                  className="field-collapse"
                  items={[
                    {
                      key: index,
                      label: (
                        <div className="field-collapse-header">
                          <span className="field-name-label">{field.fieldName || '未命名字段'}</span>
                          <span className="field-type-label">{field.fieldType}</span>
                          {field.isRequired && <span className="field-required-badge">必填</span>}
                        </div>
                      ),
                      extra: (
                        <Button
                          type="text"
                          danger
                          size="small"
                          icon={<DeleteOutlined />}
                          onClick={(e) => {
                            e.stopPropagation();
                            handleDeleteMapField(index);
                          }}
                        />
                      ),
                      children: (
                        <div className="field-config-form">
                          <div className="field-row">
                            <div className="field-item">
                              <label>字段名称 *</label>
                              <Input
                                placeholder="字段名称"
                                value={field.fieldName}
                                onChange={e => handleUpdateMapField(index, 'fieldName', e.target.value)}
                              />
                            </div>
                            <div className="field-item">
                              <label>字段类型 *</label>
                              <Select
                                value={field.fieldType}
                                onChange={value => handleUpdateMapField(index, 'fieldType', value)}
                                style={{ width: '100%' }}
                              >
                                <Select.Option value={FieldType.String}>字符串</Select.Option>
                                <Select.Option value={FieldType.Number}>数字</Select.Option>
                                <Select.Option value={FieldType.Boolean}>布尔值</Select.Option>
                                <Select.Option value={FieldType.Object}>对象</Select.Option>
                                <Select.Option value={FieldType.Array}>数组</Select.Option>
                                <Select.Option value={FieldType.Map}>Map</Select.Option>
                              </Select>
                            </div>
                          </div>

                          <div className="field-row">
                            <div className="field-item">
                              <label>字段来源 *</label>
                              <Select
                                value={field.expressionType || FieldExpressionType.Constant}
                                onChange={value => handleUpdateMapField(index, 'expressionType', value)}
                                style={{ width: '100%' }}
                              >
                                <Select.Option value={FieldExpressionType.Constant}>常量值</Select.Option>
                                <Select.Option value={FieldExpressionType.Variable}>变量引用</Select.Option>
                                <Select.Option value={FieldExpressionType.Expression}>表达式</Select.Option>
                                <Select.Option value={FieldExpressionType.NodeOutput}>节点输出</Select.Option>
                                <Select.Option value={FieldExpressionType.Context}>上下文</Select.Option>
                              </Select>
                            </div>
                            <div className="field-item">
                              <label>是否必填</label>
                              <Switch
                                checked={field.isRequired}
                                onChange={checked => handleUpdateMapField(index, 'isRequired', checked)}
                                checkedChildren="必填"
                                unCheckedChildren="可选"
                              />
                            </div>
                          </div>

                          <div className="field-row">
                            <div className="field-item full-width">
                              <label>字段值</label>
                              {renderValueInput(field, index)}
                            </div>
                          </div>

                          <div className="field-row">
                            <div className="field-item full-width">
                              <label>字段描述</label>
                              <Input.TextArea
                                placeholder="字段描述"
                                value={field.description}
                                onChange={e => handleUpdateMapField(index, 'description', e.target.value)}
                                rows={2}
                              />
                            </div>
                          </div>
                        </div>
                      ),
                    },
                  ]}
                />
              ))
            )}
          </div>
        </div>
      </Form>

      <div className="node-config-footer">
        <Space>
          <Button onClick={onCancel}>取消</Button>
          <Button type="primary" onClick={handleSave}>
            保存配置
          </Button>
        </Space>
      </div>
    </div>
  );
}

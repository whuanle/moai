import React, { useState, useEffect } from 'react';
import { Card, Table, Button, Spin, message, Form, Input, Modal, Space, Typography, Row, Col, InputNumber, Switch, Select } from 'antd';
import { GetApiClient } from '../ServiceClient';
import { 
  QuerySystemSettingsCommandResponse,
  QuerySystemSettingsCommandResponseItem,
  SetSystemSettingsCommand,
  KeyValueString
} from '../../apiClient/models';
import { proxyFormRequestError, proxyRequestError } from "../../helper/RequestError";

const { Title, Text } = Typography;
const { Option } = Select;

export default function SystemSettings() {
  const [settings, setSettings] = useState<QuerySystemSettingsCommandResponseItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [editModalVisible, setEditModalVisible] = useState(false);
  const [currentSetting, setCurrentSetting] = useState<QuerySystemSettingsCommandResponseItem | null>(null);
  const [form] = Form.useForm();
  const [submitting, setSubmitting] = useState(false);
  const [messageApi, contextHolder] = message.useMessage();

  // 获取系统设置
  const fetchSettings = async () => {
    setLoading(true);
    try {
      const client = GetApiClient();
      const response = await client.api.admin.settings.get();
      if (response?.items) {
        setSettings(response.items);
      }
    } catch (error) {
      proxyRequestError(error, messageApi);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchSettings();
  }, []);

  // 处理编辑点击
  const handleEditClick = (setting: QuerySystemSettingsCommandResponseItem) => {
    setCurrentSetting(setting);
    form.setFieldsValue({
      key: setting.key,
      value: setting.value,
      description: setting.description
    });
    setEditModalVisible(true);
  };

  // 处理编辑提交
  const handleEditSubmit = async (values: any) => {
    if (!currentSetting) return;
    
    setSubmitting(true);
    try {
      const client = GetApiClient();
      const updateCommand: SetSystemSettingsCommand = {
        settings: {
          key: values.key,
          value: values.value
        }
      };
      
      await client.api.admin.settings.put(updateCommand);
      messageApi.success('设置更新成功');
      setEditModalVisible(false);
      fetchSettings(); // 重新获取设置
    } catch (error) {
      proxyFormRequestError(error, messageApi, form);
    } finally {
      setSubmitting(false);
    }
  };

  // 判断设置值的类型并渲染相应的输入组件
  const renderValueInput = (setting: QuerySystemSettingsCommandResponseItem) => {
    const value = setting.value;
    
    // 尝试判断值的类型
    if (value === 'true' || value === 'false') {
      return (
        <Switch
          checked={value === 'true'}
          onChange={(checked) => {
            const newSettings = settings.map(s => 
              s.id === setting.id ? { ...s, value: checked.toString() } : s
            );
            setSettings(newSettings);
          }}
        />
      );
    }
    
    // 检查是否为数字
    if (!isNaN(Number(value)) && value !== '') {
      return (
        <InputNumber
          value={Number(value) || 0}
          onChange={(numValue) => {
            const newSettings = settings.map(s => 
              s.id === setting.id ? { ...s, value: numValue?.toString() || '' } : s
            );
            setSettings(newSettings);
          }}
          style={{ width: '100%' }}
        />
      );
    }
    
    // 默认为文本输入
    return (
      <Input
        value={value || ''}
        onChange={(e) => {
          const newSettings = settings.map(s => 
            s.id === setting.id ? { ...s, value: e.target.value } : s
          );
          setSettings(newSettings);
        }}
      />
    );
  };

  const columns = [
    {
      title: '配置项',
      dataIndex: 'key',
      key: 'key',
      width: 200,
    },
    {
      title: '描述',
      dataIndex: 'description',
      key: 'description',
      width: 300,
    },
    {
      title: '当前值',
      dataIndex: 'value',
      key: 'value',
      render: (value: string, record: QuerySystemSettingsCommandResponseItem) => (
        <div style={{ maxWidth: 300, wordBreak: 'break-all' }}>
          {renderValueInput(record)}
        </div>
      ),
    },
    {
      title: '操作',
      key: 'action',
      width: 120,
      render: (text: string, record: QuerySystemSettingsCommandResponseItem) => (
        <Space size="middle">
          <Button 
            type="link" 
            size="small"
            onClick={() => handleEditClick(record)}
          >
            编辑
          </Button>
        </Space>
      ),
    },
  ];

  return (
    <>
      {contextHolder}
      <Card>
        <div style={{ marginBottom: 16 }}>
          <Title level={4}>系统设置</Title>
          <Text type="secondary">管理系统配置参数，修改后立即生效</Text>
        </div>
        
        <Table
          columns={columns}
          dataSource={settings}
          rowKey="id"
          loading={loading}
          pagination={false}
          size="middle"
        />
      </Card>

      {/* 编辑设置模态框 */}
      <Modal
        title="编辑系统设置"
        open={editModalVisible}
        onCancel={() => setEditModalVisible(false)}
        footer={null}
        destroyOnClose
      >
        <Form
          form={form}
          layout="vertical"
          onFinish={handleEditSubmit}
        >
          <Form.Item
            label="配置项"
            name="key"
            rules={[{ required: true, message: '请输入配置项名称' }]}
          >
            <Input disabled />
          </Form.Item>
          
          <Form.Item
            label="描述"
            name="description"
          >
            <Input.TextArea rows={2} disabled />
          </Form.Item>
          
          <Form.Item
            label="配置值"
            name="value"
            rules={[{ required: true, message: '请输入配置值' }]}
          >
            <Input.TextArea rows={3} />
          </Form.Item>
          
          <Form.Item>
            <Space>
              <Button type="primary" htmlType="submit" loading={submitting}>
                保存
              </Button>
              <Button onClick={() => setEditModalVisible(false)}>
                取消
              </Button>
            </Space>
          </Form.Item>
        </Form>
      </Modal>
    </>
  );
} 
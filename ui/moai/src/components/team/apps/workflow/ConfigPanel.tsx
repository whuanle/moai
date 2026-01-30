/**
 * 节点配置面板 - 重构版
 */

import { Drawer, Button, Form, Input, Select, Space, message } from 'antd';
import { CloseOutlined } from '@ant-design/icons';
import { useWorkflowStore } from './store';
import { NodeType } from './types';
import './ConfigPanel.css';

interface ConfigPanelProps {
  nodeId: string;
  onClose: () => void;
}

export function ConfigPanel({ nodeId, onClose }: ConfigPanelProps) {
  const [form] = Form.useForm();
  const [messageApi, contextHolder] = message.useMessage();
  const store = useWorkflowStore();
  
  const node = store.getNode(nodeId);
  
  if (!node) {
    return null;
  }

  const handleSave = async () => {
    try {
      const values = await form.validateFields();
      
      store.updateNode(nodeId, {
        name: values.name,
        description: values.description,
        config: {
          ...node.config,
          settings: {
            ...node.config.settings,
            ...values.settings,
          },
        },
      });
      
      messageApi.success('配置已保存');
      onClose();
    } catch (error) {
      console.error('保存配置失败:', error);
      messageApi.error('保存配置失败');
    }
  };

  return (
    <>
      {contextHolder}
      <Drawer
        title={
          <Space>
            <span>节点配置</span>
            <Button 
              type="text" 
              size="small" 
              icon={<CloseOutlined />} 
              onClick={onClose} 
            />
          </Space>
        }
        placement="right"
        width={400}
        open={true}
        onClose={onClose}
        closable={false}
        maskClosable={false}
        footer={
          <Space style={{ float: 'right' }}>
            <Button onClick={onClose}>取消</Button>
            <Button type="primary" onClick={handleSave}>
              保存
            </Button>
          </Space>
        }
      >
        <Form
          form={form}
          layout="vertical"
          initialValues={{
            name: node.name,
            description: node.description,
            settings: node.config.settings,
          }}
        >
          <Form.Item
            label="节点名称"
            name="name"
            rules={[{ required: true, message: '请输入节点名称' }]}
          >
            <Input placeholder="请输入节点名称" />
          </Form.Item>

          <Form.Item
            label="节点描述"
            name="description"
          >
            <Input.TextArea 
              placeholder="请输入节点描述" 
              rows={3}
            />
          </Form.Item>

          {/* 根据节点类型显示不同的配置项 */}
          {renderNodeConfig(node.type, node.config.settings)}
        </Form>
      </Drawer>
    </>
  );
}

/**
 * 根据节点类型渲染配置项
 */
function renderNodeConfig(type: NodeType, settings: Record<string, any>) {
  switch (type) {
    case NodeType.Start:
      return (
        <Form.Item
          label="启动参数"
          name={['settings', 'parameters']}
          tooltip="工作流启动时的初始参数"
        >
          <Input.TextArea 
            placeholder="JSON 格式的参数" 
            rows={4}
          />
        </Form.Item>
      );
      
    case NodeType.AiChat:
      return (
        <>
          <Form.Item
            label="AI 模型"
            name={['settings', 'modelId']}
            rules={[{ required: true, message: '请选择 AI 模型' }]}
          >
            <Select placeholder="请选择 AI 模型">
              <Select.Option value="gpt-4">GPT-4</Select.Option>
              <Select.Option value="gpt-3.5">GPT-3.5</Select.Option>
              <Select.Option value="claude">Claude</Select.Option>
            </Select>
          </Form.Item>
          
          <Form.Item
            label="系统提示词"
            name={['settings', 'systemPrompt']}
          >
            <Input.TextArea 
              placeholder="请输入系统提示词" 
              rows={4}
            />
          </Form.Item>
        </>
      );
      
    case NodeType.JavaScript:
      return (
        <Form.Item
          label="JavaScript 代码"
          name={['settings', 'code']}
          rules={[{ required: true, message: '请输入 JavaScript 代码' }]}
        >
          <Input.TextArea 
            placeholder="// 编写 JavaScript 代码\nreturn input;" 
            rows={10}
            style={{ fontFamily: 'monospace' }}
          />
        </Form.Item>
      );
      
    case NodeType.Plugin:
      return (
        <Form.Item
          label="插件"
          name={['settings', 'pluginId']}
          rules={[{ required: true, message: '请选择插件' }]}
        >
          <Select placeholder="请选择插件">
            {/* TODO: 从 API 加载插件列表 */}
          </Select>
        </Form.Item>
      );
      
    case NodeType.Wiki:
      return (
        <Form.Item
          label="知识库"
          name={['settings', 'wikiId']}
          rules={[{ required: true, message: '请选择知识库' }]}
        >
          <Select placeholder="请选择知识库">
            {/* TODO: 从 API 加载知识库列表 */}
          </Select>
        </Form.Item>
      );
      
    case NodeType.Condition:
      return (
        <Form.Item
          label="条件表达式"
          name={['settings', 'condition']}
          rules={[{ required: true, message: '请输入条件表达式' }]}
          tooltip="支持 JavaScript 表达式，返回 true 或 false"
        >
          <Input.TextArea 
            placeholder="例如: input.value > 100" 
            rows={3}
          />
        </Form.Item>
      );
      
    default:
      return (
        <div style={{ 
          padding: '20px', 
          textAlign: 'center', 
          color: 'var(--color-text-secondary)' 
        }}>
          该节点类型暂无配置项
        </div>
      );
  }
}

/**
 * 节点配置面板 - 重构版
 */

import { useState, useRef, useEffect } from 'react';
import { Drawer, Button, Form, Input, Select, message, Tabs } from 'antd';
import { useClientContext } from '@flowgram.ai/free-layout-editor';
import { useWorkflowStore } from './store';
import { NodeType } from './types';
import { ParameterConfig, flattenFields } from './ParameterConfig';
import { getNodeTemplate } from './constants';
import './ConfigPanel.css';

interface ConfigPanelProps {
  nodeId: string;
  onClose: () => void;
}

export function ConfigPanel({ nodeId, onClose }: ConfigPanelProps) {
  // 所有 Hooks 必须在组件顶层调用
  const [basicForm] = Form.useForm();
  const [inputForm] = Form.useForm();
  const [outputForm] = Form.useForm();
  const [messageApi, contextHolder] = message.useMessage();
  const store = useWorkflowStore();
  const { document: editorDocument } = useClientContext();
  const [drawerWidth, setDrawerWidth] = useState(600);
  const [isResizing, setIsResizing] = useState(false);
  const resizeRef = useRef<HTMLDivElement>(null);
  
  const node = store.getNode(nodeId);
  const template = node ? getNodeTemplate(node.type) : null;

  // 拖动调整宽度
  useEffect(() => {
    const handleMouseMove = (e: MouseEvent) => {
      if (!isResizing) return;
      const newWidth = window.innerWidth - e.clientX;
      // 限制宽度范围：最小 600px，最大为屏幕宽度的 60%
      const maxWidth = Math.min(800, window.innerWidth * 0.6);
      if (newWidth >= 600 && newWidth <= maxWidth) {
        setDrawerWidth(newWidth);
      }
    };

    const handleMouseUp = () => {
      setIsResizing(false);
    };

    if (isResizing) {
      window.document.addEventListener('mousemove', handleMouseMove);
      window.document.addEventListener('mouseup', handleMouseUp);
    }

    return () => {
      window.document.removeEventListener('mousemove', handleMouseMove);
      window.document.removeEventListener('mouseup', handleMouseUp);
    };
  }, [isResizing]);

  // 早期返回必须在所有 Hooks 之后
  if (!node || !template) {
    return null;
  }

  const handleSave = async () => {
    try {
      // 获取所有表单的值
      const basicValues = await basicForm.validateFields();
      const inputValues = inputForm.getFieldsValue();
      const outputValues = outputForm.getFieldsValue();
      
      // 验证节点 key 唯一性
      if (basicValues.nodeKey !== node.id) {
        const existingNode = store.workflow?.nodes.find(n => n.id === basicValues.nodeKey && n.id !== nodeId);
        if (existingNode) {
          messageApi.error('节点 Key 已存在，请使用唯一的 Key');
          return;
        }
      }
      
      // 扁平化输入输出字段
      const flatInputFields = inputValues.inputFields ? flattenFields(inputValues.inputFields) : {};
      const flatOutputFields = outputValues.outputFields ? flattenFields(outputValues.outputFields) : {};
      
      // 如果 key 改变了，需要更新节点 ID
      const newNodeId = basicValues.nodeKey;
      const keyChanged = newNodeId !== nodeId;
      
      // 更新 store 中的节点数据（只更新内存，不保存到后端）
      store.updateNode(nodeId, {
        id: newNodeId,
        name: basicValues.name,
        description: basicValues.description,
        config: {
          inputFields: inputValues.inputFields || node.config.inputFields,
          outputFields: outputValues.outputFields || node.config.outputFields,
          settings: {
            ...node.config.settings,
            ...basicValues.settings,
            // 保存扁平化的字段定义（用于后端）
            flatInputFields,
            flatOutputFields,
          },
        },
      });
      
      // 同步更新编辑器中的节点显示
      if (editorDocument) {
        const editorNode = (editorDocument as any).getNodeByID?.(nodeId);
        if (editorNode) {
          // 更新节点的显示数据
          (editorDocument as any).updateNode?.(editorNode, {
            data: {
              title: basicValues.name,
              content: basicValues.description,
              inputFields: inputValues.inputFields || node.config.inputFields,
              outputFields: outputValues.outputFields || node.config.outputFields,
            },
          });
          
          // 如果 key 改变了，更新节点 ID
          if (keyChanged) {
            // 注意：编辑器可能不支持直接修改节点 ID
            // 这里我们只更新 store，编辑器的 ID 会在下次保存时同步
            console.log('⚠️ 节点 Key 已更改，将在保存工作流时同步到编辑器');
          }
        }
      }
      
      messageApi.success('配置已保存到暂存区，点击"保存"按钮保存工作流');
      onClose();
    } catch (error) {
      console.error('保存配置失败:', error);
      messageApi.error('保存配置失败');
    }
  };

  // 渲染节点特定配置（作为 JSX 元素而不是函数）
  const renderNodeSpecificConfig = () => {
    switch (node.type) {
      case NodeType.Start:
        return (
          <div className="config-section">
            <div className="config-section-title">开始节点配置</div>
            <div className="config-panel-empty">
              开始节点是工作流的入口点，可以在"输出参数"标签页中配置启动参数
            </div>
          </div>
        );
      
      case NodeType.End:
        return (
          <div className="config-section">
            <div className="config-section-title">结束节点配置</div>
            <div className="config-panel-empty">
              结束节点是工作流的结束点，可以在"输入参数"标签页中配置返回结果
            </div>
          </div>
        );
        
      case NodeType.AiChat:
        return (
          <div className="config-section">
            <div className="config-section-title">AI 配置</div>
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
          </div>
        );
        
      case NodeType.JavaScript:
        return (
          <div className="config-section">
            <div className="config-section-title">代码配置</div>
            <Form.Item
              label="JavaScript 代码"
              name={['settings', 'code']}
              rules={[{ required: true, message: '请输入 JavaScript 代码' }]}
            >
              <Input.TextArea 
                placeholder="// 编写 JavaScript 代码\nreturn input;" 
                rows={10}
                className="code-textarea"
              />
            </Form.Item>
          </div>
        );
        
      case NodeType.Plugin:
        return (
          <div className="config-section">
            <div className="config-section-title">插件配置</div>
            <Form.Item
              label="插件"
              name={['settings', 'pluginId']}
              rules={[{ required: true, message: '请选择插件' }]}
            >
              <Select placeholder="请选择插件">
                {/* TODO: 从 API 加载插件列表 */}
              </Select>
            </Form.Item>
          </div>
        );
        
      case NodeType.Wiki:
        return (
          <div className="config-section">
            <div className="config-section-title">知识库配置</div>
            <Form.Item
              label="知识库"
              name={['settings', 'wikiId']}
              rules={[{ required: true, message: '请选择知识库' }]}
            >
              <Select placeholder="请选择知识库">
                {/* TODO: 从 API 加载知识库列表 */}
              </Select>
            </Form.Item>
          </div>
        );
        
      case NodeType.Condition:
        return (
          <div className="config-section">
            <div className="config-section-title">条件配置</div>
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
          </div>
        );
        
      default:
        return (
          <div className="config-panel-empty">
            该节点类型暂无配置项
          </div>
        );
    }
  };

  return (
    <>
      {contextHolder}
      <Drawer
        title={
          <div className="config-panel-header">
            <div className="config-panel-header-icon config-panel-icon" data-color={template.color}>
              {template.icon}
            </div>
            <div className="config-panel-header-text">
              <div className="config-panel-header-title">节点配置</div>
              <div className="config-panel-header-subtitle">{template.name}</div>
            </div>
          </div>
        }
        placement="right"
        width={drawerWidth}
        open={true}
        onClose={onClose}
        closable={true}
        maskClosable={false}
        footer={
          <div className="config-panel-footer">
            <Button onClick={onClose}>取消</Button>
            <Button type="primary" onClick={handleSave}>
              保存配置
            </Button>
          </div>
        }
      >
        {/* 拖动调整宽度的手柄 */}
        <div
          ref={resizeRef}
          className={`config-panel-resize-handle ${isResizing ? 'resizing' : ''}`}
          onMouseDown={() => setIsResizing(true)}
        />
        <Tabs
          defaultActiveKey="basic"
          items={[
            {
              key: 'basic',
              label: '基本信息',
              children: (
                <Form
                  form={basicForm}
                  layout="vertical"
                  initialValues={{
                    nodeKey: node.id,
                    name: node.name,
                    description: node.description,
                    settings: node.config.settings,
                  }}
                >
                  <div className="config-section">
                    <div className="config-section-title">基本信息</div>
                    <Form.Item
                      label="节点 Key"
                      name="nodeKey"
                      rules={[
                        { required: true, message: '请输入节点 Key' },
                        { pattern: /^[a-zA-Z0-9_-]+$/, message: 'Key 只能包含字母、数字、下划线和横线' }
                      ]}
                      tooltip={
                        node.type === NodeType.Start || node.type === NodeType.End
                          ? "开始节点和结束节点的 Key 不可修改"
                          : "节点的唯一标识符，用于在工作流中引用此节点"
                      }
                    >
                      <Input 
                        placeholder="例如: start_node" 
                        disabled={node.type === NodeType.Start || node.type === NodeType.End}
                      />
                    </Form.Item>

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
                  </div>

                  {/* 根据节点类型显示不同的配置项 */}
                  {renderNodeSpecificConfig()}
                </Form>
              ),
            },
            // 输入参数标签页 - 所有节点都显示
            {
              key: 'input',
              label: '输入参数',
              children: node.type === NodeType.Start ? (
                // 开始节点的输入参数是固定的，不可修改
                <div className="config-section">
                  <div className="config-section-title">输入参数（固定）</div>
                  <div className="config-panel-fixed-params">
                    <div className="fixed-param-item">
                      <span className="fixed-param-name">input</span>
                      <span className="fixed-param-type">Map</span>
                      <span className="fixed-param-desc">工作流输入参数，由调用方传入</span>
                    </div>
                  </div>
                  <div className="config-panel-hint">
                    开始节点的输入参数是固定的，不可修改。
                  </div>
                </div>
              ) : (
                // 其他节点的输入参数可编辑
                <Form
                  form={inputForm}
                  layout="vertical"
                  initialValues={{
                    inputFields: node.config.inputFields,
                  }}
                >
                  <Form.Item name="inputFields">
                    <ParameterConfig title="输入参数配置" />
                  </Form.Item>
                </Form>
              ),
            },
            // 输出参数标签页 - 所有节点都显示
            {
              key: 'output',
              label: '输出参数',
              children: (
                <Form
                  form={outputForm}
                  layout="vertical"
                  initialValues={{
                    outputFields: node.config.outputFields,
                  }}
                >
                  <Form.Item name="outputFields">
                    <ParameterConfig title="输出参数配置" isOutput={true} />
                  </Form.Item>
                </Form>
              ),
            },
          ]}
        />
      </Drawer>
    </>
  );
}

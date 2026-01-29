/**
 * 集成核心架构的工作流配置组件示例
 * 
 * 这个文件展示如何将新的核心架构集成到现有的工作流编辑器中
 */

import { Button, Space, Typography, message, Alert } from "antd";
import { ArrowLeftOutlined, SaveOutlined, PlayCircleOutlined, WarningOutlined } from "@ant-design/icons";
import { useParams, useNavigate } from "react-router";
import { useEffect, useMemo } from "react";
import { 
  FreeLayoutEditorProvider, 
  EditorRenderer,
  useClientContext,
} from "@flowgram.ai/free-layout-editor";
import "@flowgram.ai/free-layout-editor/index.css";
import { useEditorProps } from "./useEditorProps";
import { NodePanel } from "./NodePanel";
import { NodeTemplate, NodeType } from "./types";
import { Tools } from "./Tools";
import { Minimap } from "./Minimap";
import { useWorkflowStore } from "./useWorkflowStore";
import { toEditorFormat, syncEditorChanges } from "./workflowConverter";
import "./WorkflowConfig.css";

const { Title } = Typography;

// 工具栏组件
function WorkflowTools() {
  const { document } = useClientContext();
  const [messageApi] = message.useMessage();
  const store = useWorkflowStore();

  const handleSave = async () => {
    try {
      // 同步画布数据到 store
      const currentDoc = document.toJSON();
      const { backend, canvas } = syncEditorChanges(
        currentDoc,
        store.backend,
        store.canvas
      );
      
      // 更新 store（这里简化处理，实际应该添加批量更新方法）
      store.loadFromBackend(backend);
      
      // 验证工作流
      const errors = store.validate();
      if (errors.length > 0) {
        errors.forEach(error => {
          messageApi.error(error.message);
        });
        return;
      }
      
      // 保存到后端
      const success = await store.save();
      if (success) {
        messageApi.success("工作流已保存");
      } else {
        messageApi.error("保存工作流失败");
      }
    } catch (error) {
      console.error("保存失败:", error);
      messageApi.error("保存工作流失败");
    }
  };

  const handleRun = () => {
    try {
      // 验证工作流
      const errors = store.validate();
      if (errors.length > 0) {
        messageApi.error("工作流验证失败，请修复错误后再运行");
        return;
      }
      
      const workflowData = store.toWorkflowData();
      console.log("执行工作流:", workflowData);
      messageApi.info("工作流执行功能开发中");
      // TODO: 调用 API 执行工作流
    } catch (error) {
      console.error("执行失败:", error);
      messageApi.error("执行工作流失败");
    }
  };

  return (
    <Space>
      {store.isDirty && (
        <span style={{ color: '#faad14', fontSize: 12 }}>
          <WarningOutlined /> 未保存
        </span>
      )}
      <Button 
        icon={<PlayCircleOutlined />} 
        onClick={handleRun}
        disabled={store.validationErrors.length > 0}
      >
        运行
      </Button>
      <Button 
        type="primary" 
        icon={<SaveOutlined />} 
        onClick={handleSave}
        loading={store.isSaving}
      >
        保存
      </Button>
    </Space>
  );
}

// 画布组件 - 处理拖放
function WorkflowCanvas() {
  const { playground, document } = useClientContext();
  const [messageApi] = message.useMessage();
  const store = useWorkflowStore();

  /**
   * 处理拖拽悬停事件
   */
  const handleDragOver = (e: React.DragEvent) => {
    e.preventDefault();
    e.dataTransfer.dropEffect = 'copy';
  };

  /**
   * 处理拖放事件
   * 使用 store 的 addNode 方法，自动验证约束
   */
  const handleDrop = (e: React.DragEvent) => {
    e.preventDefault();
    
    try {
      const templateData = e.dataTransfer.getData('application/json');
      if (!templateData) return;
      
      const template: NodeTemplate = JSON.parse(templateData);
      
      // 将鼠标位置转换为画布坐标
      const canvasPos = playground.config.getPosFromMouseEvent(e.nativeEvent);
      
      // 使用 store 添加节点（自动验证约束）
      const result = store.addNode(template.type as NodeType, canvasPos);
      
      if (typeof result === 'string') {
        // 成功，result 是新节点 ID
        // 同步到画布编辑器
        const editorData = toEditorFormat(store.backend, store.canvas);
        const newNode = editorData.nodes.find(n => n.id === result);
        if (newNode) {
          document.createWorkflowNode(newNode);
        }
        messageApi.success(`已添加 ${template.name} 节点`);
      } else {
        // 失败，显示错误信息
        messageApi.error(result.error);
      }
    } catch (error) {
      console.error('添加节点失败:', error);
      messageApi.error('添加节点失败');
    }
  };

  return (
    <div 
      className="workflow-editor"
      onDrop={handleDrop}
      onDragOver={handleDragOver}
    >
      <EditorRenderer />
      <Minimap />
      <Tools />
    </div>
  );
}

// 验证错误提示组件
function ValidationErrors() {
  const store = useWorkflowStore();
  
  if (store.validationErrors.length === 0) {
    return null;
  }
  
  return (
    <Alert
      type="error"
      message="工作流验证失败"
      description={
        <ul style={{ margin: 0, paddingLeft: 20 }}>
          {store.validationErrors.map((error, index) => (
            <li key={index}>{error.message}</li>
          ))}
        </ul>
      }
      style={{ marginBottom: 16 }}
      closable
    />
  );
}

export default function WorkflowConfigWithStore() {
  const { id } = useParams();
  const navigate = useNavigate();
  const teamId = parseInt(id!);
  const [messageApi, contextHolder] = message.useMessage();
  const store = useWorkflowStore();

  // 初始化工作流
  useEffect(() => {
    // TODO: 从后端加载工作流数据
    // const workflowData = await fetchWorkflow(workflowId);
    // store.loadFromBackend(workflowData);
    
    // 临时：初始化默认工作流
    store.loadFromBackend({
      id: 'workflow_1',
      name: '示例工作流',
      version: '1.0.0',
      nodes: [
        {
          id: 'start_0',
          type: NodeType.Start,
          name: '开始',
          description: '工作流开始节点',
          config: {
            inputFields: [],
            outputFields: [
              { 
                fieldName: 'trigger', 
                fieldType: 'object' as const, 
                isRequired: false,
                description: '触发器数据'
              }
            ],
            settings: {},
          },
          execution: {
            timeout: 30000,
            retryCount: 0,
            errorHandling: 'stop',
          },
        },
        {
          id: 'end_0',
          type: NodeType.End,
          name: '结束',
          description: '工作流结束节点',
          config: {
            inputFields: [
              { 
                fieldName: 'result', 
                fieldType: 'dynamic' as const, 
                isRequired: false,
                description: '工作流执行结果'
              }
            ],
            outputFields: [],
            settings: {},
          },
          execution: {
            timeout: 30000,
            retryCount: 0,
            errorHandling: 'stop',
          },
        },
      ],
      edges: [],
    });
  }, []);

  // 转换为编辑器格式
  const initialDocument = useMemo(() => {
    return toEditorFormat(store.backend, store.canvas);
  }, [store.backend, store.canvas]);

  const editorProps = useEditorProps(initialDocument);

  const handleBack = () => {
    if (store.isDirty) {
      if (!confirm('有未保存的更改，确定要离开吗？')) {
        return;
      }
    }
    navigate(`/app/team/${teamId}/manage_apps`);
  };

  return (
    <div className="workflow-config-container">
      {contextHolder}
      <FreeLayoutEditorProvider {...editorProps}>
        {/* 头部 */}
        <div className="workflow-config-header">
          <Space>
            <Button type="text" icon={<ArrowLeftOutlined />} onClick={handleBack} />
            <Title level={4} style={{ margin: 0 }}>
              {store.backend.name}
            </Title>
          </Space>
          <WorkflowTools />
        </div>

        {/* 验证错误提示 */}
        <div style={{ padding: '16px 16px 0' }}>
          <ValidationErrors />
        </div>

        {/* 画布区域 */}
        <div className="workflow-canvas-container">
          <NodePanel />
          <WorkflowCanvas />
        </div>
      </FreeLayoutEditorProvider>
    </div>
  );
}

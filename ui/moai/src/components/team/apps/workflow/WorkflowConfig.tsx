import { Button, Space, Typography, message, Spin, Empty, Tag } from "antd";
import { ArrowLeftOutlined, SaveOutlined, PlayCircleOutlined, CloseOutlined } from "@ant-design/icons";
import { useParams, useNavigate } from "react-router";
import { useEffect, useState, useCallback, useMemo } from "react";
import { 
  FreeLayoutEditorProvider, 
  EditorRenderer,
  useClientContext,
  WorkflowJSON,
} from "@flowgram.ai/free-layout-editor";
import "@flowgram.ai/free-layout-editor/index.css";
import { useEditorProps } from "./useEditorProps";
import { NodePanel } from "./NodePanel";
import { NodeTemplate } from "./types";
import { Tools } from "./Tools";
import { Minimap } from "./Minimap";
import { useWorkflowStore } from "./useWorkflowStore";
import { toEditorFormat, syncEditorChanges } from "./workflowConverter";
import { proxyRequestError } from "../../../../helper/RequestError";
import { NodeType } from "./types";
import { StartNodeConfig } from "./nodes/StartNodeConfig";
import "./WorkflowConfig.css";

const { Title } = Typography;

// 工具栏组件
function WorkflowTools({ messageApi }: { messageApi: ReturnType<typeof message.useMessage>[0] }) {
  const { document } = useClientContext();
  const store = useWorkflowStore();

  const handleSave = async () => {
    try {
      // 从编辑器获取最新数据并同步到 store
      const currentData = document.toJSON();
      
      // 使用 syncEditorChanges 同步数据
      const { backend, canvas } = syncEditorChanges(
        currentData,
        store.backend,
        store.canvas
      );
      
      // 更新 store（直接调用 set 方法）
      useWorkflowStore.setState({
        backend,
        canvas,
        isDirty: true,
      });
      
      // 保存到 API
      const success = await store.saveToApi();
      
      if (success) {
        messageApi.success("工作流已保存");
      } else {
        messageApi.error("保存工作流失败");
      }
    } catch (error) {
      console.error("保存失败:", error);
      proxyRequestError(error, messageApi, "保存工作流失败");
    }
  };

  const handleRun = () => {
    try {
      messageApi.info("工作流执行功能开发中");
      // TODO: 调用 API 执行工作流
    } catch (error) {
      console.error("执行失败:", error);
      messageApi.error("执行工作流失败");
    }
  };

  return (
    <Space>
      <Button icon={<PlayCircleOutlined />} onClick={handleRun}>
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
      {store.isDraft && <Tag color="orange">草稿</Tag>}
      {store.isDirty && <Tag color="red">未保存</Tag>}
    </Space>
  );
}

// 画布组件 - 处理拖放
function WorkflowCanvas({ messageApi }: { messageApi: ReturnType<typeof message.useMessage>[0] }) {
  const { playground, document } = useClientContext();
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
   * 从节点面板拖放节点到画布上
   */
  const handleDrop = (e: React.DragEvent) => {
    e.preventDefault();
    
    try {
      const templateData = e.dataTransfer.getData('application/json');
      if (!templateData) return;
      
      const template: NodeTemplate = JSON.parse(templateData);
      
      // 检查是否已存在开始节点或结束节点
      const existingNodes = document.toJSON().nodes || [];
      
      if (template.type === 'start') {
        const hasStartNode = existingNodes.some((node: any) => node.type === 'start');
        if (hasStartNode) {
          messageApi.warning('工作流中已存在开始节点，不能重复添加');
          return;
        }
      }
      
      if (template.type === 'end') {
        const hasEndNode = existingNodes.some((node: any) => node.type === 'end');
        if (hasEndNode) {
          messageApi.warning('工作流中已存在结束节点，不能重复添加');
          return;
        }
      }
      
      // 将鼠标位置转换为画布坐标
      const canvasPos = playground.config.getPosFromMouseEvent(e.nativeEvent);
      
      // 使用 createWorkflowNode 创建节点
      document.createWorkflowNode({
        id: `${template.type}_${Date.now()}`,
        type: template.type,
        meta: {
          position: canvasPos,
        },
        data: template.defaultData,
        blocks: [],
        edges: []
      });
      
      messageApi.success(`已添加 ${template.name} 节点`);
    } catch (error) {
      console.error('添加节点失败:', error);
      messageApi.error('添加节点失败');
    }
  };

  // 检查是否为空画布
  const isEmpty = store.backend.nodes.length === 0;

  return (
    <div 
      className="workflow-editor"
      onDrop={handleDrop}
      onDragOver={handleDragOver}
    >
      <EditorRenderer />
      {isEmpty && (
        <div className="workflow-empty-hint">
          <div className="empty-hint-content">
            <div className="empty-hint-icon">📋</div>
            <h3>开始设计你的工作流</h3>
            <p>从左侧节点面板拖拽节点到画布上</p>
          </div>
        </div>
      )}
      <Minimap />
      <Tools />
    </div>
  );
}

// 右侧配置面板组件
function ConfigSidebar({ 
  nodeId, 
  nodeType, 
  onClose,
  messageApi
}: { 
  nodeId: string; 
  nodeType: NodeType; 
  onClose: () => void;
  messageApi: ReturnType<typeof message.useMessage>[0];
}) {
  const store = useWorkflowStore();
  
  const backendNode = store.backend.nodes.find(n => n.id === nodeId);
  
  if (!backendNode) {
    return (
      <div className="workflow-config-sidebar">
        <div className="config-sidebar-header">
          <h3>节点配置</h3>
          <Button type="text" icon={<CloseOutlined />} onClick={onClose} />
        </div>
        <div className="config-sidebar-content">
          <Empty description="节点不存在" />
        </div>
      </div>
    );
  }

  const handleSaveConfig = (config: any) => {
    store.updateNodeConfig(nodeId, config);
    messageApi.success('配置已保存');
    onClose();
  };

  let configComponent = null;
  
  if (nodeType === NodeType.Start) {
    configComponent = (
      <StartNodeConfig
        nodeId={nodeId}
        config={backendNode.config}
        onSave={handleSaveConfig}
        onCancel={onClose}
      />
    );
  } else {
    // 其他节点类型的配置组件
    configComponent = (
      <div className="node-config-panel">
        <div className="node-config-header">
          <h3>{backendNode.name} 配置</h3>
          <p className="node-config-desc">该节点配置功能开发中</p>
        </div>
        <div className="node-config-footer">
          <Button onClick={onClose}>关闭</Button>
        </div>
      </div>
    );
  }

  return (
    <div className="workflow-config-sidebar">
      <div className="config-sidebar-header">
        <h3>节点配置</h3>
        <Button type="text" icon={<CloseOutlined />} onClick={onClose} />
      </div>
      <div className="config-sidebar-content">
        {configComponent}
      </div>
    </div>
  );
}

export default function WorkflowConfig() {
  const { id, appId } = useParams();
  const navigate = useNavigate();
  const [messageApi, contextHolder] = message.useMessage();
  const store = useWorkflowStore();
  const [isInitialized, setIsInitialized] = useState(false);
  const [selectedNodeId, setSelectedNodeId] = useState<string | null>(null);
  const [selectedNodeType, setSelectedNodeType] = useState<NodeType | null>(null);

  // 所有 hooks 必须在条件判断之前调用
  const teamId = id ? parseInt(id) : 0;

  const loadWorkflowData = async () => {
    if (!appId || !teamId || isNaN(teamId)) {
      console.error('无效的参数:', { appId, teamId });
      messageApi.error('无效的参数');
      return;
    }

    try {
      console.log('开始加载工作流:', { appId, teamId });
      await store.loadFromApi(appId, teamId);
      console.log('工作流加载成功，当前状态:', {
        backend: store.backend,
        canvas: store.canvas,
      });
      setIsInitialized(true);
    } catch (error) {
      console.error('加载工作流失败:', error);
      proxyRequestError(error, messageApi, '加载工作流失败');
    }
  };

  // 组件挂载时加载工作流
  useEffect(() => {
    if (appId && teamId && !isNaN(teamId)) {
      loadWorkflowData();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [appId, teamId]);

  // 未保存更改警告
  useEffect(() => {
    const handleBeforeUnload = (e: BeforeUnloadEvent) => {
      if (store.isDirty) {
        e.preventDefault();
        e.returnValue = '您有未保存的更改，确定要离开吗？';
      }
    };

    window.addEventListener('beforeunload', handleBeforeUnload);
    return () => window.removeEventListener('beforeunload', handleBeforeUnload);
  }, [store.isDirty]);

  // 准备编辑器数据 - 只在初始化后调用
  const initialDocument = useMemo(() => {
    if (!isInitialized) {
      return { nodes: [], edges: [] };
    }
    return toEditorFormat(store.backend, store.canvas);
  }, [isInitialized, store.backend, store.canvas]);
  
  // 处理编辑器内容变更
  const handleContentChange = useCallback((data: WorkflowJSON) => {
    // 从编辑器数据同步到 store
    const { backend, canvas } = syncEditorChanges(
      data,
      store.backend,
      store.canvas
    );
    
    useWorkflowStore.setState({
      backend,
      canvas,
      isDirty: true,
    });
  }, [store]);
  
  const editorProps = useEditorProps(initialDocument, setSelectedNodeId, setSelectedNodeType, handleContentChange);

  // 验证参数 - 在所有 hooks 之后
  if (!id || !appId) {
    return (
      <div className="workflow-config-container">
        {contextHolder}
        <div className="workflow-error-container">
          <Empty
            description="缺少必要参数"
            image={Empty.PRESENTED_IMAGE_SIMPLE}
          >
            <Button type="primary" onClick={() => navigate(-1)}>
              返回
            </Button>
          </Empty>
        </div>
      </div>
    );
  }

  const handleBack = () => {
    navigate(`/app/team/${teamId}/manage_apps`);
  };

  // 加载状态
  if (store.isLoading) {
    return (
      <div className="workflow-config-container">
        {contextHolder}
        <div className="workflow-loading-container">
          <Spin size="large" tip="加载工作流中..." />
        </div>
      </div>
    );
  }

  // 错误状态
  if (store.loadError) {
    return (
      <div className="workflow-config-container">
        {contextHolder}
        <div className="workflow-error-container">
          <Empty
            description={store.loadError}
            image={Empty.PRESENTED_IMAGE_SIMPLE}
          >
            <Button type="primary" onClick={loadWorkflowData}>
              重试
            </Button>
          </Empty>
        </div>
      </div>
    );
  }

  // 未初始化
  if (!isInitialized) {
    return (
      <div className="workflow-config-container">
        {contextHolder}
        <div className="workflow-loading-container">
          <Spin size="large" tip="初始化中..." />
        </div>
      </div>
    );
  }

  return (
    <div className="workflow-config-container">
      {contextHolder}
      {/* 使用 key 强制重新挂载编辑器，确保数据加载后能正确渲染 */}
      <FreeLayoutEditorProvider key={isInitialized ? 'initialized' : 'loading'} {...editorProps}>
        {/* 头部 */}
        <div className="workflow-config-header">
          <Space>
            <Button type="text" icon={<ArrowLeftOutlined />} onClick={handleBack} />
            <Title level={4} className="workflow-config-title">
              {store.backend.name || '流程编排配置'}
            </Title>
          </Space>
          <WorkflowTools messageApi={messageApi} />
        </div>

        {/* 画布区域 */}
        <div className="workflow-canvas-container">
          <NodePanel />
          <WorkflowCanvas messageApi={messageApi} />
          
          {/* 右侧配置面板 */}
          {selectedNodeId && selectedNodeType && (
            <ConfigSidebar
              nodeId={selectedNodeId}
              nodeType={selectedNodeType}
              messageApi={messageApi}
              onClose={() => {
                setSelectedNodeId(null);
                setSelectedNodeType(null);
              }}
            />
          )}
        </div>
      </FreeLayoutEditorProvider>
    </div>
  );
}

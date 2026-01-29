import { Button, Space, Typography, message } from "antd";
import { ArrowLeftOutlined, SaveOutlined, PlayCircleOutlined } from "@ant-design/icons";
import { useParams, useNavigate } from "react-router";
import { useMemo } from "react";
import { 
  FreeLayoutEditorProvider, 
  EditorRenderer,
  useClientContext,
} from "@flowgram.ai/free-layout-editor";
import "@flowgram.ai/free-layout-editor/index.css";
import { useEditorProps } from "./useEditorProps";
import { NodePanel } from "./NodePanel";
import { NodeTemplate } from "./nodeTemplates";
import { Tools } from "./Tools";
import { Minimap } from "./Minimap";
import "./WorkflowConfig.css";

const { Title } = Typography;

// 工具栏组件
function WorkflowTools() {
  const { document } = useClientContext();
  const [messageApi] = message.useMessage();

  const handleSave = () => {
    try {
      const currentDoc = document.toJSON();
      messageApi.success("工作流已保存");
      console.log("保存的工作流数据:", currentDoc);
      // TODO: 调用 API 保存到后端
    } catch (error) {
      console.error("保存失败:", error);
      messageApi.error("保存工作流失败");
    }
  };

  const handleRun = () => {
    try {
      const currentDoc = document.toJSON();
      console.log("执行工作流:", currentDoc);
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
      <Button type="primary" icon={<SaveOutlined />} onClick={handleSave}>
        保存
      </Button>
    </Space>
  );
}

// 画布组件 - 处理拖放
function WorkflowCanvas() {
  const { playground, document } = useClientContext();
  const [messageApi] = message.useMessage();

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
      
      // 将鼠标位置转换为画布坐标
      const canvasPos = playground.config.getPosFromMouseEvent(e.nativeEvent);
      
      // 创建新节点
      const newNode = {
        id: `${template.type}_${Date.now()}`,
        type: template.type,
        meta: {
          position: canvasPos,
        },
        data: template.defaultData
      };
      
      // 添加到画布
      document.addNode(newNode);
      
      messageApi.success(`已添加 ${template.name} 节点`);
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

export default function WorkflowConfig() {
  const { id } = useParams();
  const navigate = useNavigate();
  const teamId = parseInt(id!);
  const [messageApi, contextHolder] = message.useMessage();

  // 默认工作流数据
  const initialDocument = useMemo(() => ({
    nodes: [
      {
        id: "start_0",
        type: "start",
        meta: {
          position: { x: 100, y: 200 },
        },
        data: {
          title: "开始",
          content: "工作流开始节点",
        },
      },
      {
        id: "llm_0",
        type: "llm",
        meta: {
          position: { x: 400, y: 200 },
        },
        data: {
          title: "AI 处理",
          content: "使用 AI 模型处理数据",
        },
      },
      {
        id: "condition_0",
        type: "condition",
        meta: {
          position: { x: 700, y: 200 },
        },
        data: {
          title: "条件判断",
          content: "根据条件分支处理",
        },
      },
      {
        id: "action_0",
        type: "action",
        meta: {
          position: { x: 1000, y: 100 },
        },
        data: {
          title: "操作 A",
          content: "执行操作 A",
        },
      },
      {
        id: "action_1",
        type: "action",
        meta: {
          position: { x: 1000, y: 300 },
        },
        data: {
          title: "操作 B",
          content: "执行操作 B",
        },
      },
      {
        id: "end_0",
        type: "end",
        meta: {
          position: { x: 1300, y: 200 },
        },
        data: {
          title: "结束",
          content: "工作流结束节点",
        },
      },
    ],
    edges: [
      {
        sourceNodeID: "start_0",
        targetNodeID: "llm_0",
      },
      {
        sourceNodeID: "llm_0",
        targetNodeID: "condition_0",
      },
      {
        sourceNodeID: "condition_0",
        targetNodeID: "action_0",
      },
      {
        sourceNodeID: "condition_0",
        targetNodeID: "action_1",
      },
      {
        sourceNodeID: "action_0",
        targetNodeID: "end_0",
      },
      {
        sourceNodeID: "action_1",
        targetNodeID: "end_0",
      },
    ],
  }), []);

  const editorProps = useEditorProps(initialDocument);

  const handleBack = () => {
    navigate(`/app/team/${teamId}/manage_apps`);
  };

  return (
    <div className="workflow-config-container">
      {contextHolder}
      {/* 头部 */}
      <div className="workflow-config-header">
        <Space>
          <Button type="text" icon={<ArrowLeftOutlined />} onClick={handleBack} />
          <Title level={4} style={{ margin: 0 }}>
            流程编排配置
          </Title>
        </Space>
        <FreeLayoutEditorProvider {...editorProps}>
          <WorkflowTools />
        </FreeLayoutEditorProvider>
      </div>

      {/* 画布区域 */}
      <div className="workflow-canvas-container">
        <NodePanel />
        <FreeLayoutEditorProvider {...editorProps}>
          <WorkflowCanvas />
        </FreeLayoutEditorProvider>
      </div>
    </div>
  );
}

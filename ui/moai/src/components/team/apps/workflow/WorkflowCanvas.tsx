/**
 * 工作流画布组件
 * 接收拖拽的节点并创建节点实例
 */

import { useState } from 'react';
import { message } from 'antd';
import { NodeTemplate, NodeType } from './types';
import './WorkflowCanvas.css';

interface WorkflowNode {
  id: string;
  type: NodeType;
  position: { x: number; y: number };
  data: NodeTemplate['defaultData'];
  color: string;
  icon: string;
  name: string;
}

export function WorkflowCanvas() {
  const [nodes, setNodes] = useState<WorkflowNode[]>([]);
  const [messageApi, contextHolder] = message.useMessage();

  // 处理拖拽悬停
  const handleDragOver = (e: React.DragEvent) => {
    e.preventDefault();
    e.dataTransfer.dropEffect = 'copy';
  };

  // 处理节点放置
  const handleDrop = (e: React.DragEvent) => {
    e.preventDefault();
    
    try {
      const templateData = e.dataTransfer.getData('application/json');
      if (!templateData) {
        return;
      }
      
      const template: NodeTemplate = JSON.parse(templateData);
      
      // 获取画布相对位置
      const canvas = e.currentTarget as HTMLElement;
      const rect = canvas.getBoundingClientRect();
      const x = e.clientX - rect.left;
      const y = e.clientY - rect.top;
      
      // 创建新节点
      const newNode: WorkflowNode = {
        id: `${template.type}_${Date.now()}`,
        type: template.type,
        position: { x, y },
        data: template.defaultData,
        color: template.color,
        icon: template.icon,
        name: template.name
      };
      
      setNodes(prev => [...prev, newNode]);
      messageApi.success(`已添加节点: ${template.name}`);
      
    } catch (error) {
      console.error('创建节点失败:', error);
      messageApi.error('创建节点失败');
    }
  };

  // 删除节点
  const handleDeleteNode = (nodeId: string) => {
    setNodes(prev => prev.filter(n => n.id !== nodeId));
    messageApi.info('节点已删除');
  };

  return (
    <>
      {contextHolder}
      <div 
        className="workflow-canvas"
        onDragOver={handleDragOver}
        onDrop={handleDrop}
      >
        {nodes.length === 0 ? (
          <div className="canvas-empty-state">
            <div className="empty-icon">📋</div>
            <div className="empty-text">从左侧拖拽节点到此处开始构建工作流</div>
          </div>
        ) : (
          nodes.map(node => (
            <div
              key={node.id}
              className="workflow-node"
              data-node-id={node.id}
              style={{
                left: node.position.x - 75,
                top: node.position.y - 40,
                '--node-color': node.color
              } as React.CSSProperties}
            >
              <div 
                className="node-header"
                style={{ '--header-bg': node.color } as React.CSSProperties}
              >
                <span className="node-icon">{node.icon}</span>
                <span className="node-title">{node.data.title}</span>
                <button 
                  className="node-delete"
                  onClick={() => handleDeleteNode(node.id)}
                  title="删除节点"
                >
                  ×
                </button>
              </div>
              <div className="node-body">
                <div className="node-type">{node.name}</div>
                {node.data.inputFields && node.data.inputFields.length > 0 && (
                  <div className="node-ports">
                    <div className="port-label">输入:</div>
                    {node.data.inputFields.map(field => (
                      <div key={field.fieldName} className="port-item">
                        • {field.fieldName}
                      </div>
                    ))}
                  </div>
                )}
                {node.data.outputFields && node.data.outputFields.length > 0 && (
                  <div className="node-ports">
                    <div className="port-label">输出:</div>
                    {node.data.outputFields.map(field => (
                      <div key={field.fieldName} className="port-item">
                        • {field.fieldName}
                      </div>
                    ))}
                  </div>
                )}
              </div>
            </div>
          ))
        )}
      </div>
    </>
  );
}

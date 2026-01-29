/**
 * 工作流编辑器主组件
 * 整合节点面板和画布
 */

import { NodePanel } from './NodePanel';
import { WorkflowCanvas } from './WorkflowCanvas';
import './WorkflowEditor.css';

export function WorkflowEditor() {
  return (
    <div className="workflow-editor-container">
      <div className="workflow-editor-header">
        <h2>工作流编辑器</h2>
        <div className="header-actions">
          <span className="status-text">未保存</span>
        </div>
      </div>
      
      <div className="workflow-editor-content">
        <NodePanel />
        <WorkflowCanvas />
      </div>
    </div>
  );
}

/**
 * 工具栏组件 - 增强版
 * 包含缩放、撤销/重做、连线类型切换、保存等功能
 */

import { useEffect, useState } from 'react';
import { Button, Tooltip, Divider } from 'antd';
import { 
  ZoomInOutlined, 
  ZoomOutOutlined,
  FullscreenOutlined,
  AimOutlined,
  UndoOutlined,
  RedoOutlined,
  LineOutlined,
  BranchesOutlined,
  SaveOutlined
} from '@ant-design/icons';
import { useClientContext, usePlaygroundTools } from '@flowgram.ai/free-layout-editor';
import { useSaveWorkflow } from './hooks';
import './Toolbar.css';

export function Toolbar() {
  const { history, document } = useClientContext();
  const tools = usePlaygroundTools();
  const { handleSave, saving, contextHolder } = useSaveWorkflow();
  const [canUndo, setCanUndo] = useState(false);
  const [canRedo, setCanRedo] = useState(false);
  const [edgeType, setEdgeType] = useState<'straight' | 'bezier'>('bezier');
  const [zoom, setZoom] = useState(100);

  // 监听历史记录变化
  useEffect(() => {
    if (!history || !history.undoRedoService) {
      return;
    }
    
    const disposable = history.undoRedoService.onChange(() => {
      setCanUndo(history.canUndo());
      setCanRedo(history.canRedo());
    });
    return () => disposable.dispose();
  }, [history]);

  // 监听缩放变化
  useEffect(() => {
    if (tools && tools.zoom) {
      setZoom(Math.floor(tools.zoom * 100));
    }
  }, [tools?.zoom]);

  const handleZoomIn = () => {
    if (tools && tools.zoomin) {
      tools.zoomin();
    }
  };

  const handleZoomOut = () => {
    if (tools && tools.zoomout) {
      tools.zoomout();
    }
  };

  const handleFitView = () => {
    if (tools && tools.fitView) {
      tools.fitView();
    }
  };

  const handleCenter = () => {
    if (document && document.fitView) {
      document.fitView(false);
    }
  };

  const handleUndo = () => {
    if (canUndo && history) {
      history.undo();
    }
  };

  const handleRedo = () => {
    if (canRedo && history) {
      history.redo();
    }
  };

  const handleToggleEdgeType = () => {
    const newType = edgeType === 'straight' ? 'bezier' : 'straight';
    setEdgeType(newType);
  };

  return (
    <>
      {contextHolder}
      <div className="workflow-toolbar">
      {/* 缩放控制 */}
      <Tooltip title="放大">
        <Button 
          type="text" 
          icon={<ZoomInOutlined />} 
          onClick={handleZoomIn}
        />
      </Tooltip>
      
      <Tooltip title="缩小">
        <Button 
          type="text" 
          icon={<ZoomOutOutlined />} 
          onClick={handleZoomOut}
        />
      </Tooltip>
      
      <span className="workflow-toolbar-zoom">{zoom}%</span>
      
      <Divider type="vertical" style={{ margin: '0 4px' }} />
      
      {/* 视图控制 */}
      <Tooltip title="适应画布">
        <Button 
          type="text" 
          icon={<FullscreenOutlined />} 
          onClick={handleFitView}
        />
      </Tooltip>
      
      <Tooltip title="居中">
        <Button 
          type="text" 
          icon={<AimOutlined />} 
          onClick={handleCenter}
        />
      </Tooltip>
      
      <Divider type="vertical" style={{ margin: '0 4px' }} />
      
      {/* 历史记录 */}
      <Tooltip title="撤销">
        <Button 
          type="text" 
          icon={<UndoOutlined />} 
          onClick={handleUndo}
          disabled={!canUndo}
        />
      </Tooltip>
      
      <Tooltip title="重做">
        <Button 
          type="text" 
          icon={<RedoOutlined />} 
          onClick={handleRedo}
          disabled={!canRedo}
        />
      </Tooltip>
      
      <Divider type="vertical" style={{ margin: '0 4px' }} />
      
      {/* 连线类型切换 */}
      <Tooltip title={edgeType === 'straight' ? '切换为曲线' : '切换为直线'}>
        <Button 
          type="text" 
          icon={edgeType === 'straight' ? <BranchesOutlined /> : <LineOutlined />} 
          onClick={handleToggleEdgeType}
        />
      </Tooltip>
      
      <Divider type="vertical" style={{ margin: '0 4px' }} />
      
      {/* 保存按钮 */}
      <Tooltip title="保存工作流">
        <Button 
          type="primary" 
          icon={<SaveOutlined />} 
          onClick={handleSave}
          loading={saving}
          size="small"
        >
          保存
        </Button>
      </Tooltip>
    </div>
    </>
  );
}

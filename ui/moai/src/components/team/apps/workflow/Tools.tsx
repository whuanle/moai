import React, { useEffect, useState } from 'react';
import { Button, Space } from 'antd';
import {
  ZoomInOutlined,
  ZoomOutOutlined,
  CompressOutlined,
  UndoOutlined,
  RedoOutlined,
  BranchesOutlined,
  NodeIndexOutlined,
  ApartmentOutlined,
} from '@ant-design/icons';
import { usePlaygroundTools, useClientContext, LineType } from '@flowgram.ai/free-layout-editor';

/**
 * 工具栏组件
 * 提供缩放、适应视图、自动布局、撤销/重做、线条类型切换等功能
 */
export const Tools: React.FC = () => {
  const { history } = useClientContext();
  const tools = usePlaygroundTools();
  const [canUndo, setCanUndo] = useState(false);
  const [canRedo, setCanRedo] = useState(false);

  useEffect(() => {
    const disposable = history.undoRedoService.onChange(() => {
      setCanUndo(history.canUndo());
      setCanRedo(history.canRedo());
    });
    return () => disposable.dispose();
  }, [history]);

  // 切换线条类型
  const handleToggleLineType = () => {
    const newLineType = tools.lineType === LineType.BEZIER ? LineType.LINE_CHART : LineType.BEZIER;
    tools.switchLineType(newLineType);
  };

  // 判断当前是否为曲线
  const isBezier = tools.lineType === LineType.BEZIER;

  return (
    <div className="workflow-tools">
      <Space>
        <Button
          type="text"
          icon={<ZoomInOutlined />}
          onClick={() => tools.zoomin()}
          title="放大 (Ctrl +)"
        />
        <Button
          type="text"
          icon={<ZoomOutOutlined />}
          onClick={() => tools.zoomout()}
          title="缩小 (Ctrl -)"
        />
        <Button
          type="text"
          icon={<CompressOutlined />}
          onClick={() => tools.fitView()}
          title="适应画布"
        />
        <Button
          type="text"
          icon={<BranchesOutlined />}
          onClick={() => tools.autoLayout()}
          title="自动布局"
        />
        <Button
          type="text"
          icon={isBezier ? <ApartmentOutlined /> : <NodeIndexOutlined />}
          onClick={handleToggleLineType}
          title={isBezier ? '切换到折线' : '切换到曲线'}
        />
        <Button
          type="text"
          icon={<UndoOutlined />}
          onClick={() => history.undo()}
          disabled={!canUndo}
          title="撤销 (Ctrl Z)"
        />
        <Button
          type="text"
          icon={<RedoOutlined />}
          onClick={() => history.redo()}
          disabled={!canRedo}
          title="重做 (Ctrl Shift Z)"
        />
        <span className="workflow-zoom-level">{Math.floor(tools.zoom * 100)}%</span>
      </Space>
    </div>
  );
};

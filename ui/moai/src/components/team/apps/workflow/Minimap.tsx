import React from 'react';
import { MinimapRender } from '@flowgram.ai/minimap-plugin';

/**
 * 小地图组件
 * 显示整个工作流的缩略图，方便快速导航
 */
export const Minimap: React.FC = () => {
  return (
    <div className="workflow-minimap">
      <MinimapRender />
    </div>
  );
};

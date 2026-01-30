/**
 * 缩略图组件 - 小画布
 * 显示在左下角
 */

import { MinimapRender } from '@flowgram.ai/minimap-plugin';
import './Minimap.css';

export function Minimap() {
  return (
    <div className="workflow-minimap">
      <MinimapRender
        containerStyles={{
          pointerEvents: 'auto',
          position: 'relative',
          top: 'unset',
          right: 'unset',
          bottom: 'unset',
          left: 'unset',
        }}
        inactiveStyle={{
          opacity: 1,
          scale: 1,
          translateX: 0,
          translateY: 0,
        }}
      />
    </div>
  );
}

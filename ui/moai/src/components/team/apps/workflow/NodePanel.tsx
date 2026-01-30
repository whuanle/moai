/**
 * 节点面板组件 - 重构版
 */

import { useState, useMemo } from 'react';
import { Input, Collapse, Badge } from 'antd';
import type { CollapseProps } from 'antd';
import { SearchOutlined } from '@ant-design/icons';
import { 
  NODE_TEMPLATES, 
  CATEGORY_NAMES, 
  CATEGORY_ICONS,
  getAllCategories 
} from './constants';
import { NodeCategory, NodeTemplate } from './types';
import './NodePanel.css';

export function NodePanel() {
  const [searchText, setSearchText] = useState('');
  const [activeKeys, setActiveKeys] = useState<string[]>(['control', 'ai', 'data', 'integration']);

  // 过滤节点
  const filteredTemplates = useMemo(() => {
    if (!searchText) return NODE_TEMPLATES;
    
    const lowerSearch = searchText.toLowerCase();
    return NODE_TEMPLATES.filter(
      t => t.name.toLowerCase().includes(lowerSearch) ||
           t.description.toLowerCase().includes(lowerSearch)
    );
  }, [searchText]);

  // 按分类分组
  const groupedTemplates = useMemo(() => {
    const groups = new Map<NodeCategory, NodeTemplate[]>();
    
    getAllCategories().forEach(category => {
      const templates = filteredTemplates.filter(t => t.category === category);
      if (templates.length > 0) {
        groups.set(category, templates);
      }
    });
    
    return groups;
  }, [filteredTemplates]);

  // 拖拽开始
  const handleDragStart = (e: React.DragEvent, template: NodeTemplate) => {
    e.dataTransfer.effectAllowed = 'copy';
    e.dataTransfer.setData('application/json', JSON.stringify(template));
  };

  // 构建 Collapse items
  const collapseItems: CollapseProps['items'] = useMemo(() => {
    return Array.from(groupedTemplates.entries()).map(([category, templates]) => ({
      key: category,
      label: (
        <div className="category-header">
          <span className="category-icon">{CATEGORY_ICONS[category]}</span>
          <span className="category-name">{CATEGORY_NAMES[category]}</span>
          <Badge count={templates.length} showZero />
        </div>
      ),
      children: (
        <div className="node-list">
          {templates.map(template => (
            <div
              key={template.type}
              className="node-item"
              draggable
              onDragStart={e => handleDragStart(e, template)}
              data-color={template.color}
            >
              <div 
                className="node-item-indicator" 
                style={{ '--node-color': template.color } as React.CSSProperties}
              />
              <div className="node-item-icon">{template.icon}</div>
              <div className="node-item-content">
                <div className="node-item-name">{template.name}</div>
                <div className="node-item-desc">{template.description}</div>
              </div>
            </div>
          ))}
        </div>
      ),
    }));
  }, [groupedTemplates]);

  return (
    <div className="node-panel">
      <div className="node-panel-header">
        <h3>节点库</h3>
        <Input
          placeholder="搜索节点"
          prefix={<SearchOutlined />}
          value={searchText}
          onChange={e => setSearchText(e.target.value)}
          allowClear
        />
      </div>

      <div className="node-panel-content">
        <Collapse 
          activeKey={activeKeys}
          onChange={keys => setActiveKeys(keys as string[])}
          ghost
          items={collapseItems}
        />

        {filteredTemplates.length === 0 && (
          <div className="node-panel-empty">
            <p>未找到匹配的节点</p>
          </div>
        )}
      </div>
    </div>
  );
}

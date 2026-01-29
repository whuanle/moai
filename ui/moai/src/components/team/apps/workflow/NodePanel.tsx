/**
 * 工作流节点面板组件
 * 显示可拖拽的节点模板库
 */

import { useState } from 'react';
import { Collapse, Input, Badge } from 'antd';
import { SearchOutlined } from '@ant-design/icons';
import { nodeTemplates, categoryNames } from './nodeTemplates';
import { NodeCategory, NodeTemplate } from './types';
import './NodePanel.css';

export function NodePanel() {
  const [searchText, setSearchText] = useState('');
  
  // 按分类分组节点
  const groupedNodes = nodeTemplates.reduce((acc, template) => {
    if (!acc[template.category]) {
      acc[template.category] = [];
    }
    acc[template.category].push(template);
    return acc;
  }, {} as Record<NodeCategory, NodeTemplate[]>);
  
  // 过滤节点
  const filteredGroups = Object.entries(groupedNodes).map(([category, nodes]) => ({
    category: category as NodeCategory,
    nodes: nodes.filter(node => 
      node.name.toLowerCase().includes(searchText.toLowerCase()) ||
      node.description.toLowerCase().includes(searchText.toLowerCase())
    )
  })).filter(group => group.nodes.length > 0);
  
  // 拖拽开始事件
  const handleDragStart = (e: React.DragEvent, template: NodeTemplate) => {
    e.dataTransfer.setData('application/json', JSON.stringify(template));
    e.dataTransfer.effectAllowed = 'copy';
    
    // 添加拖拽样式类
    const target = e.currentTarget as HTMLElement;
    target.classList.add('dragging');
    
    // 设置节点颜色（通过 CSS 变量）
    target.style.setProperty('--node-border-color', template.color);
  };
  
  // 拖拽结束事件
  const handleDragEnd = (e: React.DragEvent) => {
    const target = e.currentTarget as HTMLElement;
    target.classList.remove('dragging');
  };
  
  return (
    <div className="workflow-node-panel">
      <div className="node-panel-header">
        <h3>节点库</h3>
        <Input
          placeholder="搜索节点"
          prefix={<SearchOutlined />}
          value={searchText}
          onChange={e => setSearchText(e.target.value)}
          allowClear
          size="small"
        />
      </div>
      
      <div className="node-panel-content">
        <Collapse
          defaultActiveKey={Object.values(NodeCategory)}
          ghost
          items={filteredGroups.map(({ category, nodes }) => ({
            key: category,
            label: (
              <span className="node-category-label">
                {categoryNames[category]}
                <Badge count={nodes.length} className="category-badge" />
              </span>
            ),
            children: (
              <div className="node-template-list">
                {nodes.map(template => (
                  <div
                    key={template.type}
                    className="node-template-card"
                    draggable
                    onDragStart={e => handleDragStart(e, template)}
                    onDragEnd={handleDragEnd}
                    title={template.description}
                    ref={(el) => {
                      if (el) {
                        el.style.setProperty('--node-border-color', template.color);
                      }
                    }}
                  >
                    <div className="node-template-icon">{template.icon}</div>
                    <div className="node-template-info">
                      <div className="node-template-name">{template.name}</div>
                      <div className="node-template-desc">{template.description}</div>
                    </div>
                  </div>
                ))}
              </div>
            )
          }))}
        />
      </div>
    </div>
  );
}

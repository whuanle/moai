import { WorkflowNodeRegistry } from '@flowgram.ai/free-layout-editor';
import { nodeTemplates } from './nodeTemplates';

/**
 * 工作流节点注册表
 * 根据节点模板自动生成节点配置
 */
export const nodeRegistries: WorkflowNodeRegistry[] = nodeTemplates.map(template => {
  const registry: WorkflowNodeRegistry = {
    type: template.type,
    meta: {
      defaultExpanded: true,
    },
  };

  // 特殊节点配置
  if (template.type === 'start') {
    registry.meta = {
      ...registry.meta,
      isStart: true,
      deleteDisable: true,
      copyDisable: true,
      defaultPorts: [{ type: 'output' }],
    };
  } else if (template.type === 'end') {
    registry.meta = {
      ...registry.meta,
      deleteDisable: true,
      copyDisable: true,
      defaultPorts: [{ type: 'input' }],
    };
  } else if (template.type === 'condition') {
    registry.meta = {
      ...registry.meta,
      defaultPorts: [
        { type: 'input' },
        { type: 'output' },
        { type: 'output' },
      ],
    };
  } else if (template.type === 'fork') {
    registry.meta = {
      ...registry.meta,
      defaultPorts: [
        { type: 'input' },
        { type: 'output' },
        { type: 'output' },
      ],
    };
  } else if (template.type === 'forEach') {
    registry.meta = {
      ...registry.meta,
      defaultPorts: [
        { type: 'input' },
        { type: 'output' },
      ],
    };
  } else {
    // 默认配置：一个输入和一个输出
    registry.meta = {
      ...registry.meta,
      defaultPorts: [
        { type: 'input' },
        { type: 'output' },
      ],
    };
  }

  return registry;
});

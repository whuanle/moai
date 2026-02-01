/**
 * 工作流常量配置 - 重构版
 * 合并 nodeTemplates.ts 和 nodeRegistries.tsx
 */

import { 
  NodeType, 
  NodeCategory, 
  NodeTemplate, 
  NodeConstraints,
  FieldType 
} from './types';

// ==================== 节点约束配置 ====================

export const NODE_CONSTRAINTS: Record<NodeType, NodeConstraints> = {
  [NodeType.Start]: {
    minCount: 1,
    maxCount: 1,
    deletable: false,
    copyable: false,
    requiresInput: false,
    requiresOutput: true,
  },
  [NodeType.End]: {
    minCount: 1,
    maxCount: 1,
    deletable: false,
    copyable: false,
    requiresInput: true,
    requiresOutput: false,
  },
  [NodeType.Condition]: {
    minCount: 0,
    maxCount: -1,
    deletable: true,
    copyable: true,
    requiresInput: true,
    requiresOutput: true,
  },
  [NodeType.Fork]: {
    minCount: 0,
    maxCount: -1,
    deletable: true,
    copyable: true,
    requiresInput: true,
    requiresOutput: true,
  },
  [NodeType.ForEach]: {
    minCount: 0,
    maxCount: -1,
    deletable: true,
    copyable: true,
    requiresInput: true,
    requiresOutput: true,
  },
  [NodeType.AiChat]: {
    minCount: 0,
    maxCount: -1,
    deletable: true,
    copyable: true,
    requiresInput: true,
    requiresOutput: true,
  },
  [NodeType.DataProcess]: {
    minCount: 0,
    maxCount: -1,
    deletable: true,
    copyable: true,
    requiresInput: true,
    requiresOutput: true,
  },
  [NodeType.JavaScript]: {
    minCount: 0,
    maxCount: -1,
    deletable: true,
    copyable: true,
    requiresInput: true,
    requiresOutput: true,
  },
  [NodeType.Plugin]: {
    minCount: 0,
    maxCount: -1,
    deletable: true,
    copyable: true,
    requiresInput: true,
    requiresOutput: true,
  },
  [NodeType.Wiki]: {
    minCount: 0,
    maxCount: -1,
    deletable: true,
    copyable: true,
    requiresInput: true,
    requiresOutput: true,
  },
};

// ==================== 节点模板配置 ====================

export const NODE_TEMPLATES: NodeTemplate[] = [
  // 控制流节点
  {
    type: NodeType.Start,
    name: '开始',
    description: '工作流的入口点',
    icon: '▶️',
    color: '#52c41a',
    category: NodeCategory.Control,
    defaultData: {
      inputFields: [
        { 
          fieldName: 'input', 
          fieldType: FieldType.Map,
          expressionType: 'Run',  // 运行时传入，不可修改
          description: '工作流输入参数（固定）'
        }
      ],
      outputFields: [
        { 
          fieldName: 'output', 
          fieldType: FieldType.Map,
          expressionType: 'Run',  // 运行时传入
          description: '工作流输出参数'
        }
      ],
    }
  },
  {
    type: NodeType.End,
    name: '结束',
    description: '工作流的结束节点',
    icon: '⏹️',
    color: '#ff4d4f',
    category: NodeCategory.Control,
    defaultData: {
      inputFields: [
        { 
          fieldName: 'input', 
          fieldType: FieldType.Map,
          expressionType: 'Run',  // 从其他节点获取
          description: '工作流输入参数'
        }
      ],
      outputFields: [
        { 
          fieldName: 'output', 
          fieldType: FieldType.Map,
          expressionType: 'Run',
          description: '工作流输出结果'
        }
      ],
    }
  },
  {
    type: NodeType.Condition,
    name: '条件判断',
    description: '根据条件分支执行',
    icon: '◆',
    color: '#faad14',
    category: NodeCategory.Control,
    defaultData: {
      inputFields: [
        { 
          fieldName: 'condition', 
          fieldType: FieldType.Boolean, 
          isRequired: true,
          description: '判断条件'
        }
      ],
      outputFields: [
        { 
          fieldName: 'true', 
          fieldType: FieldType.Dynamic, 
          description: '条件为真'
        },
        { 
          fieldName: 'false', 
          fieldType: FieldType.Dynamic, 
          description: '条件为假'
        }
      ],
    }
  },
  {
    type: NodeType.Fork,
    name: '并行分支',
    description: '同时执行多个分支',
    icon: '⑂',
    color: '#722ed1',
    category: NodeCategory.Control,
    defaultData: {
      inputFields: [
        { 
          fieldName: 'input', 
          fieldType: FieldType.Dynamic, 
          description: '输入数据'
        }
      ],
      outputFields: [
        { 
          fieldName: 'branches', 
          fieldType: FieldType.Array, 
          description: '分支结果'
        }
      ],
    }
  },
  {
    type: NodeType.ForEach,
    name: '循环遍历',
    description: '遍历数组中的每个元素',
    icon: '🔁',
    color: '#13c2c2',
    category: NodeCategory.Control,
    defaultData: {
      inputFields: [
        { 
          fieldName: 'array', 
          fieldType: FieldType.Array, 
          isRequired: true,
          description: '要遍历的数组'
        }
      ],
      outputFields: [
        { 
          fieldName: 'item', 
          fieldType: FieldType.Dynamic, 
          description: '当前元素'
        },
        { 
          fieldName: 'index', 
          fieldType: FieldType.Number, 
          description: '元素索引'
        }
      ],
    }
  },
  
  // AI 节点
  {
    type: NodeType.AiChat,
    name: 'AI 对话',
    description: '调用 AI 模型进行对话',
    icon: '🤖',
    color: '#1677ff',
    category: NodeCategory.AI,
    defaultData: {
      inputFields: [
        { 
          fieldName: 'prompt', 
          fieldType: FieldType.String, 
          isRequired: true,
          description: '对话提示词'
        },
        { 
          fieldName: 'context', 
          fieldType: FieldType.String, 
          description: '上下文信息'
        }
      ],
      outputFields: [
        { 
          fieldName: 'response', 
          fieldType: FieldType.String, 
          description: 'AI 回复'
        }
      ],
    }
  },
  
  // 数据处理节点
  {
    type: NodeType.DataProcess,
    name: '数据处理',
    description: '处理和转换数据',
    icon: '⚙️',
    color: '#2f54eb',
    category: NodeCategory.Data,
    defaultData: {
      inputFields: [
        { 
          fieldName: 'input', 
          fieldType: FieldType.Dynamic, 
          isRequired: true,
          description: '输入数据'
        }
      ],
      outputFields: [
        { 
          fieldName: 'output', 
          fieldType: FieldType.Dynamic, 
          description: '处理结果'
        }
      ],
    }
  },
  {
    type: NodeType.JavaScript,
    name: 'JavaScript',
    description: '执行 JavaScript 代码',
    icon: '📜',
    color: '#f5222d',
    category: NodeCategory.Data,
    defaultData: {
      inputFields: [
        { 
          fieldName: 'input', 
          fieldType: FieldType.Dynamic, 
          description: '输入变量'
        }
      ],
      outputFields: [
        { 
          fieldName: 'output', 
          fieldType: FieldType.Dynamic, 
          description: '执行结果'
        }
      ],
      settings: {
        code: '// 编写 JavaScript 代码\nreturn input;'
      }
    }
  },
  
  // 集成节点
  {
    type: NodeType.Plugin,
    name: '插件调用',
    description: '调用已配置的插件',
    icon: '🔌',
    color: '#eb2f96',
    category: NodeCategory.Integration,
    defaultData: {
      inputFields: [
        { 
          fieldName: 'params', 
          fieldType: FieldType.Object, 
          description: '插件参数'
        }
      ],
      outputFields: [
        { 
          fieldName: 'result', 
          fieldType: FieldType.Dynamic, 
          description: '插件结果'
        }
      ],
    }
  },
  {
    type: NodeType.Wiki,
    name: '知识库查询',
    description: '从知识库中检索信息',
    icon: '📚',
    color: '#52c41a',
    category: NodeCategory.Integration,
    defaultData: {
      inputFields: [
        { 
          fieldName: 'query', 
          fieldType: FieldType.String, 
          isRequired: true,
          description: '查询关键词'
        }
      ],
      outputFields: [
        { 
          fieldName: 'documents', 
          fieldType: FieldType.Array, 
          description: '检索结果'
        }
      ],
    }
  }
];

// ==================== 分类配置 ====================

export const CATEGORY_NAMES: Record<NodeCategory, string> = {
  [NodeCategory.Control]: '控制流',
  [NodeCategory.AI]: 'AI 节点',
  [NodeCategory.Data]: '数据处理',
  [NodeCategory.Integration]: '集成'
};

export const CATEGORY_ICONS: Record<NodeCategory, string> = {
  [NodeCategory.Control]: '🎮',
  [NodeCategory.AI]: '🤖',
  [NodeCategory.Data]: '⚙️',
  [NodeCategory.Integration]: '🔌'
};

// ==================== 工具函数 ====================

/**
 * 根据类型获取节点模板
 */
export function getNodeTemplate(type: NodeType): NodeTemplate | undefined {
  return NODE_TEMPLATES.find(t => t.type === type);
}

/**
 * 根据分类获取节点模板
 */
export function getNodeTemplatesByCategory(category: NodeCategory): NodeTemplate[] {
  return NODE_TEMPLATES.filter(t => t.category === category);
}

/**
 * 获取所有分类
 */
export function getAllCategories(): NodeCategory[] {
  return Object.values(NodeCategory);
}

/**
 * 生成节点 ID
 */
export function generateNodeId(type: NodeType): string {
  // 开始节点和结束节点使用固定的 key
  if (type === NodeType.Start) {
    return 'start';
  }
  if (type === NodeType.End) {
    return 'end';
  }
  return `${type}_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
}

/**
 * 生成连接 ID
 */
export function generateEdgeId(source: string, target: string): string {
  return `edge_${source}_${target}_${Date.now()}`;
}

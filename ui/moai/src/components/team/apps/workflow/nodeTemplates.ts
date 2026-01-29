/**
 * 工作流节点模板定义
 * 定义所有可用的节点类型、分类和默认配置
 */

/**
 * 节点分类枚举
 */
export enum NodeCategory {
  Control = 'control',      // 控制流
  AI = 'ai',               // AI 节点
  Data = 'data',           // 数据处理
  Integration = 'integration' // 集成
}

/**
 * 字段定义接口
 */
export interface FieldDefine {
  fieldName: string;
  fieldType: 'empty' | 'string' | 'number' | 'boolean' | 'object' | 'array' | 'dynamic';
  isRequired: boolean;
}

/**
 * 节点模板接口
 */
export interface NodeTemplate {
  type: string;
  name: string;
  description: string;
  icon: string;
  color: string;
  category: NodeCategory;
  defaultData: {
    title: string;
    content?: string;
    inputFields?: FieldDefine[];
    outputFields?: FieldDefine[];
  };
}

/**
 * 节点模板数组
 * 包含所有可用的节点类型定义
 */
export const nodeTemplates: NodeTemplate[] = [
  // ==================== 控制流节点 ====================
  {
    type: 'start',
    name: '开始',
    description: '工作流的起始节点',
    icon: '▶️',
    color: '#52c41a',
    category: NodeCategory.Control,
    defaultData: {
      title: '开始',
      outputFields: [
        { fieldName: 'trigger', fieldType: 'object', isRequired: false }
      ]
    }
  },
  {
    type: 'end',
    name: '结束',
    description: '工作流的结束节点',
    icon: '⏹️',
    color: '#ff4d4f',
    category: NodeCategory.Control,
    defaultData: {
      title: '结束',
      inputFields: [
        { fieldName: 'result', fieldType: 'dynamic', isRequired: false }
      ]
    }
  },
  {
    type: 'condition',
    name: '条件判断',
    description: '根据条件分支执行',
    icon: '◆',
    color: '#faad14',
    category: NodeCategory.Control,
    defaultData: {
      title: '条件判断',
      inputFields: [
        { fieldName: 'condition', fieldType: 'boolean', isRequired: true }
      ],
      outputFields: [
        { fieldName: 'true', fieldType: 'dynamic', isRequired: false },
        { fieldName: 'false', fieldType: 'dynamic', isRequired: false }
      ]
    }
  },
  {
    type: 'fork',
    name: '并行分支',
    description: '同时执行多个分支',
    icon: '⑂',
    color: '#722ed1',
    category: NodeCategory.Control,
    defaultData: {
      title: '并行分支',
      inputFields: [
        { fieldName: 'input', fieldType: 'dynamic', isRequired: false }
      ],
      outputFields: [
        { fieldName: 'branches', fieldType: 'array', isRequired: false }
      ]
    }
  },
  {
    type: 'forEach',
    name: '循环遍历',
    description: '遍历数组中的每个元素',
    icon: '🔁',
    color: '#13c2c2',
    category: NodeCategory.Control,
    defaultData: {
      title: '循环遍历',
      inputFields: [
        { fieldName: 'array', fieldType: 'array', isRequired: true }
      ],
      outputFields: [
        { fieldName: 'item', fieldType: 'dynamic', isRequired: false },
        { fieldName: 'index', fieldType: 'number', isRequired: false }
      ]
    }
  },
  
  // ==================== AI 节点 ====================
  {
    type: 'aiChat',
    name: 'AI 对话',
    description: '调用 AI 模型进行对话',
    icon: '🤖',
    color: '#1677ff',
    category: NodeCategory.AI,
    defaultData: {
      title: 'AI 对话',
      inputFields: [
        { fieldName: 'prompt', fieldType: 'string', isRequired: true },
        { fieldName: 'context', fieldType: 'string', isRequired: false }
      ],
      outputFields: [
        { fieldName: 'response', fieldType: 'string', isRequired: false }
      ]
    }
  },
  
  // ==================== 数据处理节点 ====================
  {
    type: 'dataProcess',
    name: '数据处理',
    description: '处理和转换数据',
    icon: '⚙️',
    color: '#2f54eb',
    category: NodeCategory.Data,
    defaultData: {
      title: '数据处理',
      inputFields: [
        { fieldName: 'input', fieldType: 'dynamic', isRequired: true }
      ],
      outputFields: [
        { fieldName: 'output', fieldType: 'dynamic', isRequired: false }
      ]
    }
  },
  {
    type: 'javaScript',
    name: 'JavaScript',
    description: '执行 JavaScript 代码',
    icon: '📜',
    color: '#f5222d',
    category: NodeCategory.Data,
    defaultData: {
      title: 'JavaScript',
      content: '// 编写 JavaScript 代码\nreturn input;',
      inputFields: [
        { fieldName: 'input', fieldType: 'dynamic', isRequired: false }
      ],
      outputFields: [
        { fieldName: 'output', fieldType: 'dynamic', isRequired: false }
      ]
    }
  },
  
  // ==================== 集成节点 ====================
  {
    type: 'plugin',
    name: '插件调用',
    description: '调用已配置的插件',
    icon: '🔌',
    color: '#eb2f96',
    category: NodeCategory.Integration,
    defaultData: {
      title: '插件调用',
      inputFields: [
        { fieldName: 'params', fieldType: 'object', isRequired: false }
      ],
      outputFields: [
        { fieldName: 'result', fieldType: 'dynamic', isRequired: false }
      ]
    }
  },
  {
    type: 'wiki',
    name: '知识库查询',
    description: '从知识库中检索信息',
    icon: '📚',
    color: '#52c41a',
    category: NodeCategory.Integration,
    defaultData: {
      title: '知识库查询',
      inputFields: [
        { fieldName: 'query', fieldType: 'string', isRequired: true }
      ],
      outputFields: [
        { fieldName: 'documents', fieldType: 'array', isRequired: false }
      ]
    }
  }
];

/**
 * 分类名称映射
 */
export const categoryNames: Record<NodeCategory, string> = {
  [NodeCategory.Control]: '控制流',
  [NodeCategory.AI]: 'AI 节点',
  [NodeCategory.Data]: '数据处理',
  [NodeCategory.Integration]: '集成'
};

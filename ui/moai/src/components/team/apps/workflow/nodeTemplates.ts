/**
 * 工作流节点模板定义
 */

import { NodeTemplate, NodeType, NodeCategory, FieldType } from './types';

export const nodeTemplates: NodeTemplate[] = [
  // ==================== 控制流节点 ====================
  {
    type: NodeType.Start,
    name: '开始',
    description: '工作流的入口点，初始化工作流上下文并传递启动参数',
    icon: '▶️',
    color: '#52c41a',
    category: NodeCategory.Control,
    defaultData: {
      title: '开始节点',
      content: '工作流的入口点，初始化工作流上下文并传递启动参数',
      inputFields: [
        { 
          fieldName: 'parameters', 
          fieldType: FieldType.Map, 
          isRequired: false,
          description: '启动参数（Map 类型，可配置子字段）'
        }
      ],
      outputFields: [
        { 
          fieldName: 'parameters', 
          fieldType: FieldType.Map, 
          isRequired: true,
          description: '传递给下一个节点的参数（Map 类型）'
        }
      ]
    }
  },
  {
    type: NodeType.End,
    name: '结束',
    description: '工作流的结束节点，输出最终结果',
    icon: '⏹️',
    color: '#ff4d4f',
    category: NodeCategory.Control,
    defaultData: {
      title: '结束节点',
      content: '工作流的结束节点，输出最终结果',
      inputFields: [
        { 
          fieldName: 'result', 
          fieldType: FieldType.Dynamic, 
          isRequired: false,
          description: '工作流执行结果'
        }
      ]
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
      title: '条件判断',
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
          isRequired: false,
          description: '条件为真时的输出'
        },
        { 
          fieldName: 'false', 
          fieldType: FieldType.Dynamic, 
          isRequired: false,
          description: '条件为假时的输出'
        }
      ]
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
      title: '并行分支',
      inputFields: [
        { 
          fieldName: 'input', 
          fieldType: FieldType.Dynamic, 
          isRequired: false,
          description: '输入数据'
        }
      ],
      outputFields: [
        { 
          fieldName: 'branches', 
          fieldType: FieldType.Array, 
          isRequired: false,
          description: '分支执行结果'
        }
      ]
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
      title: '循环遍历',
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
          isRequired: false,
          description: '当前遍历的元素'
        },
        { 
          fieldName: 'index', 
          fieldType: FieldType.Number, 
          isRequired: false,
          description: '当前元素的索引'
        }
      ]
    }
  },
  
  // ==================== AI 节点 ====================
  {
    type: NodeType.AiChat,
    name: 'AI 对话',
    description: '调用 AI 模型进行对话',
    icon: '🤖',
    color: '#1677ff',
    category: NodeCategory.AI,
    defaultData: {
      title: 'AI 对话',
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
          isRequired: false,
          description: '上下文信息'
        }
      ],
      outputFields: [
        { 
          fieldName: 'response', 
          fieldType: FieldType.String, 
          isRequired: false,
          description: 'AI 回复内容'
        }
      ]
    }
  },
  
  // ==================== 数据处理节点 ====================
  {
    type: NodeType.DataProcess,
    name: '数据处理',
    description: '处理和转换数据',
    icon: '⚙️',
    color: '#2f54eb',
    category: NodeCategory.Data,
    defaultData: {
      title: '数据处理',
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
          isRequired: false,
          description: '处理后的数据'
        }
      ]
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
      title: 'JavaScript',
      content: '// 编写 JavaScript 代码\nreturn input;',
      inputFields: [
        { 
          fieldName: 'input', 
          fieldType: FieldType.Dynamic, 
          isRequired: false,
          description: '输入变量'
        }
      ],
      outputFields: [
        { 
          fieldName: 'output', 
          fieldType: FieldType.Dynamic, 
          isRequired: false,
          description: '代码执行结果'
        }
      ]
    }
  },
  
  // ==================== 集成节点 ====================
  {
    type: NodeType.Plugin,
    name: '插件调用',
    description: '调用已配置的插件',
    icon: '🔌',
    color: '#eb2f96',
    category: NodeCategory.Integration,
    defaultData: {
      title: '插件调用',
      inputFields: [
        { 
          fieldName: 'params', 
          fieldType: FieldType.Object, 
          isRequired: false,
          description: '插件参数'
        }
      ],
      outputFields: [
        { 
          fieldName: 'result', 
          fieldType: FieldType.Dynamic, 
          isRequired: false,
          description: '插件执行结果'
        }
      ]
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
      title: '知识库查询',
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
          isRequired: false,
          description: '检索到的文档'
        }
      ]
    }
  }
];

// 分类名称映射
export const categoryNames: Record<NodeCategory, string> = {
  [NodeCategory.Control]: '控制流',
  [NodeCategory.AI]: 'AI 节点',
  [NodeCategory.Data]: '数据处理',
  [NodeCategory.Integration]: '集成'
};

// 根据类型获取节点模板
export function getNodeTemplate(type: NodeType): NodeTemplate | undefined {
  return nodeTemplates.find(t => t.type === type);
}

// 根据分类获取节点模板
export function getNodeTemplatesByCategory(category: NodeCategory): NodeTemplate[] {
  return nodeTemplates.filter(t => t.category === category);
}

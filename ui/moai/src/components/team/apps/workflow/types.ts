/**
 * 工作流节点类型定义
 */

// 节点类型枚举
export enum NodeType {
  Start = 'start',
  End = 'end',
  Condition = 'condition',
  Fork = 'fork',
  ForEach = 'forEach',
  AiChat = 'aiChat',
  DataProcess = 'dataProcess',
  JavaScript = 'javaScript',
  Plugin = 'plugin',
  Wiki = 'wiki'
}

// 节点分类
export enum NodeCategory {
  Control = 'control',      // 控制流
  AI = 'ai',               // AI 节点
  Data = 'data',           // 数据处理
  Integration = 'integration' // 集成
}

// 字段类型
export enum FieldType {
  Empty = 'empty',
  String = 'string',
  Number = 'number',
  Boolean = 'boolean',
  Object = 'object',
  Array = 'array',
  Dynamic = 'dynamic'
}

// 字段定义
export interface FieldDefine {
  fieldName: string;
  fieldType: FieldType;
  isRequired: boolean;
  description?: string;
}

// 节点模板接口
export interface NodeTemplate {
  type: NodeType;
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

/**
 * 工作流节点类型定义
 */

import type { FieldDefine as ApiFieldDefine } from '../../../apiClient/models';

// 使用 API 的 FieldDefine 类型作为基础
export type FieldDefine = ApiFieldDefine;

// 扩展的字段定义（用于工作流配置）
export interface ExtendedFieldDefine extends ApiFieldDefine {
  // 字段表达式类型（字段来源）
  expressionType?: FieldExpressionType;
  // 字段值（根据 expressionType 不同，值的含义不同）
  value?: any;
  // 子字段（当 fieldType 为 Map 或 Object 时）
  children?: ExtendedFieldDefine[];
}

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
  Map = 'map',
  Dynamic = 'dynamic'
}

// 字段表达式类型（字段来源）
export enum FieldExpressionType {
  Constant = 'constant',      // 常量值
  Variable = 'variable',      // 变量引用
  Expression = 'expression',  // 表达式
  NodeOutput = 'nodeOutput',  // 节点输出
  Context = 'context'         // 上下文
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

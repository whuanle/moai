/**
 * 参数配置组件
 * 用于配置节点的输入输出参数
 */

import { useState } from 'react';
import { Button, Input, Select, Popconfirm, Tooltip, Collapse } from 'antd';
import { PlusOutlined, DeleteOutlined } from '@ant-design/icons';
import { FieldDefine, FieldType } from './types';
import './ParameterConfig.css';

// 表达式类型选项
const EXPRESSION_TYPE_OPTIONS = [
  { value: 'Run', label: '运行时', description: '运行时传入的值' },
  { value: 'Fixed', label: '固定值', description: '固定的常数值' },
  { value: 'Variable', label: '变量', description: '变量引用（如 nodeKey.output）' },
  { value: 'Jsonpath', label: 'JSONPath', description: 'JSON 路径表达式' },
  { value: 'Interpolation', label: '插值', description: '字符串插值模板' },
];

/**
 * 标准化表达式类型值（处理大小写不一致问题）
 */
function normalizeExpressionType(value?: string): string {
  if (!value) return 'Variable';
  const normalized = value.charAt(0).toUpperCase() + value.slice(1).toLowerCase();
  // 特殊处理 Jsonpath
  if (normalized === 'Jsonpath') return 'Jsonpath';
  // 检查是否是有效值
  const validValues = ['Run', 'Fixed', 'Variable', 'Jsonpath', 'Interpolation'];
  return validValues.includes(normalized) ? normalized : 'Variable';
}

/**
 * 格式化字段名：只允许小写字母、数字、下划线，必须以字母开头
 */
function formatFieldName(name: string): string {
  let formatted = name.toLowerCase();
  formatted = formatted.replace(/[^a-z0-9_]/g, '');
  if (formatted && !/^[a-z]/.test(formatted)) {
    formatted = 'f_' + formatted;
  }
  return formatted;
}

interface ParameterConfigProps {
  value?: FieldDefine[];
  onChange?: (value: FieldDefine[]) => void;
  title?: string;
  isOutput?: boolean;  // 是否是输出参数（输出参数至少需要一个字段）
}

export function ParameterConfig({ value = [], onChange, title = '参数配置', isOutput = false }: ParameterConfigProps) {
  const [activeKeys, setActiveKeys] = useState<string[]>(value.map((_, i) => String(i)));
  
  const handleAdd = () => {
    const timestamp = Date.now().toString(36);
    const newField: FieldDefine = {
      fieldName: `field_${timestamp}`,
      fieldType: FieldType.String,
      expressionType: 'Variable',
      description: '',
    };
    onChange?.([...value, newField]);
    // 自动展开新添加的参数
    setActiveKeys([...activeKeys, String(value.length)]);
  };

  const handleRemove = (index: number) => {
    // 输出参数至少保留一个字段
    if (isOutput && value.length <= 1) {
      return;
    }
    const newValue = value.filter((_, i) => i !== index);
    onChange?.(newValue);
  };

  const handleFieldChange = (index: number, field: Partial<FieldDefine>) => {
    const newValue = [...value];
    
    if (field.fieldName !== undefined) {
      field.fieldName = formatFieldName(field.fieldName);
    }
    
    newValue[index] = { ...newValue[index], ...field };
    
    // 如果类型改变，清理不适用的属性
    if (field.fieldType) {
      if (field.fieldType === FieldType.Map) {
        // Map 类型：只能是运行时，清除子字段和值
        delete newValue[index].children;
        newValue[index].expressionType = 'Run';
        newValue[index].value = undefined;
      } else if (field.fieldType !== FieldType.Object && field.fieldType !== FieldType.Array) {
        delete newValue[index].children;
      }
    }
    
    // 如果选择了运行时，清除值和子字段
    if (field.expressionType === 'Run') {
      newValue[index].value = undefined;
      delete newValue[index].children;
    }
    
    onChange?.(newValue);
  };

  const handleAddChild = (parentIndex: number) => {
    const newValue = [...value];
    const parent = newValue[parentIndex];
    
    // 运行时类型不能添加子字段
    if (parent.expressionType === 'Run') {
      return;
    }
    
    if (!parent.children) {
      parent.children = [];
    }
    
    const isArray = parent.fieldType === FieldType.Array;
    const childIndex = parent.children.length;
    
    if (isArray) {
      // 数组类型：子字段名是序号，不能设置值
      // 新元素继承 [0] 的类型
      const firstChildType = parent.children.length > 0 
        ? parent.children[0].fieldType 
        : FieldType.String;
      parent.children.push({
        fieldName: String(childIndex),
        fieldType: firstChildType,
        expressionType: 'Fixed',
        description: '',
        isArrayIndex: true,  // 标记为数组索引
      });
    } else {
      // Object 类型：普通字段名
      const timestamp = Date.now().toString(36);
      parent.children.push({
        fieldName: `field_${timestamp}`,
        fieldType: FieldType.String,
        expressionType: 'Variable',
        description: '',
      });
    }
    
    // 添加子字段后，清除父字段的值
    parent.value = undefined;
    
    onChange?.(newValue);
  };

  const handleRemoveChild = (parentIndex: number, childIndex: number) => {
    const newValue = [...value];
    const parent = newValue[parentIndex];
    
    if (parent.children) {
      parent.children = parent.children.filter((_, i) => i !== childIndex);
    }
    
    onChange?.(newValue);
  };

  const handleChildChange = (parentIndex: number, childIndex: number, field: Partial<FieldDefine>) => {
    const newValue = [...value];
    const parent = newValue[parentIndex];
    
    if (field.fieldName !== undefined) {
      field.fieldName = formatFieldName(field.fieldName);
    }
    if (parent.children) {
      parent.children[childIndex] = { ...parent.children[childIndex], ...field };
      
      // 数组类型：当修改 [0] 的类型时，同步更新其他元素的类型
      if (parent.fieldType === FieldType.Array && childIndex === 0 && field.fieldType) {
        parent.children.forEach((child, i) => {
          if (i > 0) {
            child.fieldType = field.fieldType!;
          }
        });
      }
    }
    
    onChange?.(newValue);
  };

  const renderFieldContent = (field: FieldDefine, index: number, parentPath: string = '') => {
    const fieldPath = parentPath ? `${parentPath}.${field.fieldName}` : field.fieldName;
    const isArray = field.fieldType === FieldType.Array;
    const isObject = field.fieldType === FieldType.Object;
    const isMap = field.fieldType === FieldType.Map;
    const normalizedExprType = normalizeExpressionType(field.expressionType);
    const isRuntime = normalizedExprType === 'Run';
    const isFixed = normalizedExprType === 'Fixed';
    const hasChildren = field.children && field.children.length > 0;
    
    // Object/Array 选择变量或 JsonPath 时，不能添加子字段，只能输入值
    const canAddChildren = (isObject || isArray) && isFixed && !isRuntime && !isMap;
    
    // Object/Array 类型选择固定值时，只能通过子字段定义，不能直接设置值
    // 运行时类型也不能设置值
    // 但如果选择变量或 JsonPath，可以输入值
    const valueDisabled = isRuntime || ((isObject || isArray) && isFixed);
    
    // Object/Array 类型可选的赋值方式（不包含插值）
    // Map 类型只能选择运行时
    const expressionOptions = isMap
      ? EXPRESSION_TYPE_OPTIONS.filter(opt => opt.value === 'Run')
      : (isObject || isArray) 
        ? EXPRESSION_TYPE_OPTIONS.filter(opt => opt.value !== 'Interpolation')
        : EXPRESSION_TYPE_OPTIONS;

    return (
      <div className="parameter-field-content">
        <div className="parameter-field-row">
          <div className="parameter-field-inputs">
            <Tooltip title="只能使用小写字母、数字、下划线，必须以字母开头">
              <Input
                value={field.fieldName}
                onChange={(e) => handleFieldChange(index, { fieldName: e.target.value })}
                placeholder="field_name"
                className="parameter-field-name-input"
              />
            </Tooltip>
            <Select
              value={field.fieldType}
              onChange={(fieldType) => handleFieldChange(index, { fieldType })}
              className="parameter-field-type-select"
            >
              <Select.Option value={FieldType.String}>String</Select.Option>
              <Select.Option value={FieldType.Number}>Number</Select.Option>
              <Select.Option value={FieldType.Boolean}>Boolean</Select.Option>
              <Select.Option value={FieldType.Object}>Object</Select.Option>
              <Select.Option value={FieldType.Array}>Array</Select.Option>
              <Select.Option value={FieldType.Map}>Map</Select.Option>
              <Select.Option value={FieldType.Dynamic}>Dynamic</Select.Option>
            </Select>
            <Tooltip title="赋值方式">
              <Select
                value={normalizedExprType}
                onChange={(expressionType) => handleFieldChange(index, { expressionType })}
                className="parameter-field-expr-select"
              >
                {expressionOptions.map(opt => (
                  <Select.Option key={opt.value} value={opt.value}>
                    {opt.label}
                  </Select.Option>
                ))}
              </Select>
            </Tooltip>
          </div>
          <div className="parameter-field-actions">
            {canAddChildren && (
              <Button
                type="text"
                size="small"
                icon={<PlusOutlined />}
                onClick={(e) => { e.stopPropagation(); handleAddChild(index); }}
              >
                子字段
              </Button>
            )}
            {/* 输出参数至少保留一个字段 */}
            {!(isOutput && value.length <= 1) && (
              <Popconfirm
                title="确定删除此字段？"
                onConfirm={() => handleRemove(index)}
                okText="确定"
                cancelText="取消"
              >
                <Button
                  type="text"
                  size="small"
                  danger
                  icon={<DeleteOutlined />}
                  onClick={(e) => e.stopPropagation()}
                />
              </Popconfirm>
            )}
          </div>
        </div>
        
        {/* 字段值输入框 */}
        <Tooltip title={valueDisabled ? (isRuntime ? '运行时类型不能设置值' : '需通过子字段定义') : '字段值'}>
          <Input
            value={field.value}
            onChange={(e) => handleFieldChange(index, { value: e.target.value })}
            placeholder={valueDisabled ? (isRuntime ? '运行时传入' : '通过子字段定义') : '字段值'}
            className="parameter-field-value"
            disabled={valueDisabled}
          />
        </Tooltip>

        {isMap && (
          <div className="parameter-field-hint">
            💡 Map 类型不支持详细字段设计
          </div>
        )}
        
        {isRuntime && (
          <div className="parameter-field-hint">
            💡 运行时类型的值由调用方传入，不能手动设置
          </div>
        )}

        {hasChildren && (
          <div className="parameter-field-children">
            {field.children!.map((child, childIndex) => {
              // 数组类型：路径格式为 fieldName[0].
              const isArrayChild = isArray;
              // 数组元素：只有 [0] 可以修改类型，其他元素类型跟随 [0]
              const isFirstArrayChild = isArrayChild && childIndex === 0;
              const childNormalizedExprType = normalizeExpressionType(child.expressionType);
              const childIsRuntime = childNormalizedExprType === 'Run';
              const childIsObject = child.fieldType === FieldType.Object;
              const childIsArray = child.fieldType === FieldType.Array;
              const childIsFixed = childNormalizedExprType === 'Fixed';
              // 子字段值禁用条件：运行时 或 (Object/Array + Fixed)
              const childValueDisabled = childIsRuntime || ((childIsObject || childIsArray) && childIsFixed);
              // 子字段的赋值方式选项（Object/Array 不能选插值）
              const childExpressionOptions = (childIsObject || childIsArray)
                ? EXPRESSION_TYPE_OPTIONS.filter(opt => opt.value !== 'Interpolation')
                : EXPRESSION_TYPE_OPTIONS;
              
              return (
                <div key={childIndex} className="parameter-child-field">
                  <div className="parameter-field-header">
                    <div className="parameter-field-inputs">
                      {isArrayChild ? (
                        // 数组索引：显示为 fieldName[0].、fieldName[1]. 等，不需要额外输入框
                        <>
                          <span className="parameter-field-path">{field.fieldName}[{childIndex}].</span>
                          <Tooltip title={!isFirstArrayChild ? '数组元素类型必须与 [0] 一致' : ''}>
                            <Select
                              value={child.fieldType}
                              onChange={(fieldType) => handleChildChange(index, childIndex, { fieldType })}
                              className="parameter-field-type-select"
                              disabled={!isFirstArrayChild}
                            >
                              <Select.Option value={FieldType.String}>String</Select.Option>
                              <Select.Option value={FieldType.Number}>Number</Select.Option>
                              <Select.Option value={FieldType.Boolean}>Boolean</Select.Option>
                              <Select.Option value={FieldType.Object}>Object</Select.Option>
                              <Select.Option value={FieldType.Array}>Array</Select.Option>
                              <Select.Option value={FieldType.Dynamic}>Dynamic</Select.Option>
                            </Select>
                          </Tooltip>
                          <Select
                            value={childNormalizedExprType}
                            onChange={(expressionType) => handleChildChange(index, childIndex, { expressionType })}
                            className="parameter-field-expr-select"
                          >
                            {childExpressionOptions.map(opt => (
                              <Select.Option key={opt.value} value={opt.value}>
                                {opt.label}
                              </Select.Option>
                            ))}
                          </Select>
                        </>
                      ) : (
                        // Object 子字段：显示路径前缀 + 可编辑字段名
                        <>
                          <span className="parameter-field-path">{fieldPath}.</span>
                          <Tooltip title="只能使用小写字母、数字、下划线，必须以字母开头">
                            <Input
                              value={child.fieldName}
                              onChange={(e) => handleChildChange(index, childIndex, { fieldName: e.target.value })}
                              placeholder="field_name"
                              className="parameter-field-name-input"
                            />
                          </Tooltip>
                          <Select
                            value={child.fieldType}
                            onChange={(fieldType) => handleChildChange(index, childIndex, { fieldType })}
                            className="parameter-field-type-select"
                          >
                            <Select.Option value={FieldType.String}>String</Select.Option>
                            <Select.Option value={FieldType.Number}>Number</Select.Option>
                            <Select.Option value={FieldType.Boolean}>Boolean</Select.Option>
                            <Select.Option value={FieldType.Object}>Object</Select.Option>
                            <Select.Option value={FieldType.Array}>Array</Select.Option>
                            <Select.Option value={FieldType.Dynamic}>Dynamic</Select.Option>
                          </Select>
                          <Select
                            value={childNormalizedExprType}
                            onChange={(expressionType) => handleChildChange(index, childIndex, { expressionType })}
                            className="parameter-field-expr-select"
                          >
                            {childExpressionOptions.map(opt => (
                              <Select.Option key={opt.value} value={opt.value}>
                                {opt.label}
                              </Select.Option>
                            ))}
                          </Select>
                        </>
                      )}
                    </div>
                    <Popconfirm
                      title="确定删除此字段？"
                      onConfirm={() => handleRemoveChild(index, childIndex)}
                      okText="确定"
                      cancelText="取消"
                    >
                      <Button
                        type="text"
                        size="small"
                        danger
                        icon={<DeleteOutlined />}
                      />
                    </Popconfirm>
                  </div>
                  <Input
                    value={child.value}
                    onChange={(e) => handleChildChange(index, childIndex, { value: e.target.value })}
                    placeholder={childValueDisabled ? (childIsRuntime ? '运行时传入' : '通过子字段定义') : '字段值'}
                    className="parameter-field-value"
                    disabled={childValueDisabled}
                  />
                  {isArrayChild && !isFirstArrayChild && (
                    <div className="parameter-field-hint">
                      💡 类型跟随 [0]
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        )}
      </div>
    );
  };

  return (
    <div className="parameter-config">
      <div className="parameter-config-header">
        <h4>{title}</h4>
        <Button
          type="dashed"
          icon={<PlusOutlined />}
          onClick={handleAdd}
          block
        >
          添加参数
        </Button>
      </div>
      
      <div className="parameter-config-list">
        {value.length === 0 ? (
          <div className="parameter-config-empty">
            {isOutput ? '输出参数至少需要一个字段' : '暂无参数，点击上方按钮添加'}
          </div>
        ) : (
          <Collapse
            activeKey={activeKeys}
            onChange={(keys) => setActiveKeys(keys as string[])}
            className="parameter-collapse"
            items={value.map((field, index) => ({
              key: String(index),
              label: (
                <span className="parameter-collapse-label">
                  <span className="parameter-collapse-name">{field.fieldName || '未命名'}</span>
                  <span className="parameter-collapse-type">{field.fieldType}</span>
                </span>
              ),
              children: renderFieldContent(field, index),
            }))}
          />
        )}
      </div>
    </div>
  );
}


/**
 * 将 FieldDefine 扁平化为 a.b 格式
 */
export function flattenFields(fields: FieldDefine[], parentPath: string = ''): Record<string, FieldDefine> {
  const result: Record<string, FieldDefine> = {};
  
  fields.forEach(field => {
    const fieldPath = parentPath ? `${parentPath}.${field.fieldName}` : field.fieldName;
    
    result[fieldPath] = {
      ...field,
      children: undefined,
    };
    
    if (field.children && field.children.length > 0) {
      const childFields = flattenFields(field.children, fieldPath);
      Object.assign(result, childFields);
    }
  });
  
  return result;
}

/**
 * 从扁平化格式还原为树形结构
 */
export function unflattenFields(flatFields: Record<string, FieldDefine>): FieldDefine[] {
  const result: FieldDefine[] = [];
  const pathMap = new Map<string, FieldDefine>();
  
  const sortedPaths = Object.keys(flatFields).sort((a, b) => {
    const depthA = a.split('.').length;
    const depthB = b.split('.').length;
    return depthA - depthB;
  });
  
  sortedPaths.forEach(path => {
    const field = flatFields[path];
    const parts = path.split('.');
    
    if (parts.length === 1) {
      result.push(field);
      pathMap.set(path, field);
    } else {
      const parentPath = parts.slice(0, -1).join('.');
      const parent = pathMap.get(parentPath);
      
      if (parent) {
        if (!parent.children) {
          parent.children = [];
        }
        parent.children.push(field);
        pathMap.set(path, field);
      }
    }
  });
  
  return result;
}

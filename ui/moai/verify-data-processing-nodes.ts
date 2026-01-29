/**
 * 验证任务 1.4：定义数据处理节点模板
 * 
 * 测试 DataProcess 和 JavaScript 节点的完整性和正确性
 */

import { nodeTemplates, getNodeTemplate } from './src/components/team/apps/workflow/nodeTemplates';
import { NodeType, NodeCategory, FieldType } from './src/components/team/apps/workflow/types';

// 测试结果统计
let passed = 0;
let failed = 0;

function test(name: string, condition: boolean, errorMsg?: string) {
  if (condition) {
    console.log(`✓ ${name}`);
    passed++;
  } else {
    console.log(`✗ ${name}`);
    if (errorMsg) console.log(`  错误: ${errorMsg}`);
    failed++;
  }
}

console.log('开始验证任务 1.4：定义数据处理节点模板\n');
console.log('='.repeat(60));

// ==================== 测试 DataProcess 节点 ====================
console.log('\n【测试 DataProcess 节点】');

const dataProcessTemplate = getNodeTemplate(NodeType.DataProcess);

// 1. 节点存在性测试
test(
  '1.1 DataProcess 节点模板存在',
  dataProcessTemplate !== undefined,
  'DataProcess 节点模板未找到'
);

if (!dataProcessTemplate) {
  console.log('\n❌ DataProcess 节点模板不存在，跳过该节点测试');
} else {
  // 2. 基础属性测试
  test(
    '2.1 节点类型正确',
    dataProcessTemplate.type === NodeType.DataProcess,
    `期望 ${NodeType.DataProcess}，实际 ${dataProcessTemplate.type}`
  );

  test(
    '2.2 节点名称正确',
    dataProcessTemplate.name === '数据处理',
    `期望 "数据处理"，实际 "${dataProcessTemplate.name}"`
  );

  test(
    '2.3 节点描述正确',
    dataProcessTemplate.description === '处理和转换数据',
    `期望 "处理和转换数据"，实际 "${dataProcessTemplate.description}"`
  );

  test(
    '2.4 节点图标正确',
    dataProcessTemplate.icon === '⚙️',
    `期望 "⚙️"，实际 "${dataProcessTemplate.icon}"`
  );

  test(
    '2.5 节点颜色正确',
    dataProcessTemplate.color === '#2f54eb',
    `期望 "#2f54eb"，实际 "${dataProcessTemplate.color}"`
  );

  test(
    '2.6 节点分类为数据处理',
    dataProcessTemplate.category === NodeCategory.Data,
    `期望 ${NodeCategory.Data}，实际 ${dataProcessTemplate.category}`
  );

  // 3. 默认数据测试
  test(
    '3.1 默认数据存在',
    dataProcessTemplate.defaultData !== undefined,
    '默认数据未定义'
  );

  test(
    '3.2 默认标题正确',
    dataProcessTemplate.defaultData.title === '数据处理',
    `期望 "数据处理"，实际 "${dataProcessTemplate.defaultData.title}"`
  );

  // 4. 输入字段测试
  const inputFields = dataProcessTemplate.defaultData.inputFields || [];

  test(
    '4.1 输入字段存在',
    inputFields.length > 0,
    '输入字段为空'
  );

  test(
    '4.2 输入字段数量正确',
    inputFields.length === 1,
    `期望 1 个输入字段，实际 ${inputFields.length} 个`
  );

  // 测试 input 字段
  const inputField = inputFields.find(f => f.fieldName === 'input');
  test(
    '4.3 input 字段存在',
    inputField !== undefined,
    'input 字段未找到'
  );

  if (inputField) {
    test(
      '4.4 input 字段类型为 dynamic',
      inputField.fieldType === FieldType.Dynamic,
      `期望 ${FieldType.Dynamic}，实际 ${inputField.fieldType}`
    );

    test(
      '4.5 input 字段为必填',
      inputField.isRequired === true,
      `期望 true，实际 ${inputField.isRequired}`
    );

    test(
      '4.6 input 字段有描述',
      inputField.description !== undefined && inputField.description.length > 0,
      'input 字段缺少描述'
    );
  }

  // 5. 输出字段测试
  const outputFields = dataProcessTemplate.defaultData.outputFields || [];

  test(
    '5.1 输出字段存在',
    outputFields.length > 0,
    '输出字段为空'
  );

  test(
    '5.2 输出字段数量正确',
    outputFields.length === 1,
    `期望 1 个输出字段，实际 ${outputFields.length} 个`
  );

  // 测试 output 字段
  const outputField = outputFields.find(f => f.fieldName === 'output');
  test(
    '5.3 output 字段存在',
    outputField !== undefined,
    'output 字段未找到'
  );

  if (outputField) {
    test(
      '5.4 output 字段类型为 dynamic',
      outputField.fieldType === FieldType.Dynamic,
      `期望 ${FieldType.Dynamic}，实际 ${outputField.fieldType}`
    );

    test(
      '5.5 output 字段为非必填',
      outputField.isRequired === false,
      `期望 false，实际 ${outputField.isRequired}`
    );

    test(
      '5.6 output 字段有描述',
      outputField.description !== undefined && outputField.description.length > 0,
      'output 字段缺少描述'
    );
  }
}

// ==================== 测试 JavaScript 节点 ====================
console.log('\n【测试 JavaScript 节点】');

const jsTemplate = getNodeTemplate(NodeType.JavaScript);

// 1. 节点存在性测试
test(
  '1.1 JavaScript 节点模板存在',
  jsTemplate !== undefined,
  'JavaScript 节点模板未找到'
);

if (!jsTemplate) {
  console.log('\n❌ JavaScript 节点模板不存在，跳过该节点测试');
} else {
  // 2. 基础属性测试
  test(
    '2.1 节点类型正确',
    jsTemplate.type === NodeType.JavaScript,
    `期望 ${NodeType.JavaScript}，实际 ${jsTemplate.type}`
  );

  test(
    '2.2 节点名称正确',
    jsTemplate.name === 'JavaScript',
    `期望 "JavaScript"，实际 "${jsTemplate.name}"`
  );

  test(
    '2.3 节点描述正确',
    jsTemplate.description === '执行 JavaScript 代码',
    `期望 "执行 JavaScript 代码"，实际 "${jsTemplate.description}"`
  );

  test(
    '2.4 节点图标正确',
    jsTemplate.icon === '📜',
    `期望 "📜"，实际 "${jsTemplate.icon}"`
  );

  test(
    '2.5 节点颜色正确',
    jsTemplate.color === '#f5222d',
    `期望 "#f5222d"，实际 "${jsTemplate.color}"`
  );

  test(
    '2.6 节点分类为数据处理',
    jsTemplate.category === NodeCategory.Data,
    `期望 ${NodeCategory.Data}，实际 ${jsTemplate.category}`
  );

  // 3. 默认数据测试
  test(
    '3.1 默认数据存在',
    jsTemplate.defaultData !== undefined,
    '默认数据未定义'
  );

  test(
    '3.2 默认标题正确',
    jsTemplate.defaultData.title === 'JavaScript',
    `期望 "JavaScript"，实际 "${jsTemplate.defaultData.title}"`
  );

  test(
    '3.3 默认代码内容存在',
    jsTemplate.defaultData.content !== undefined && jsTemplate.defaultData.content.length > 0,
    'JavaScript 节点应包含默认代码内容'
  );

  test(
    '3.4 默认代码内容正确',
    jsTemplate.defaultData.content === '// 编写 JavaScript 代码\nreturn input;',
    `期望默认代码为 "// 编写 JavaScript 代码\\nreturn input;"，实际 "${jsTemplate.defaultData.content}"`
  );

  // 4. 输入字段测试
  const jsInputFields = jsTemplate.defaultData.inputFields || [];

  test(
    '4.1 输入字段存在',
    jsInputFields.length > 0,
    '输入字段为空'
  );

  test(
    '4.2 输入字段数量正确',
    jsInputFields.length === 1,
    `期望 1 个输入字段，实际 ${jsInputFields.length} 个`
  );

  // 测试 input 字段
  const jsInputField = jsInputFields.find(f => f.fieldName === 'input');
  test(
    '4.3 input 字段存在',
    jsInputField !== undefined,
    'input 字段未找到'
  );

  if (jsInputField) {
    test(
      '4.4 input 字段类型为 dynamic',
      jsInputField.fieldType === FieldType.Dynamic,
      `期望 ${FieldType.Dynamic}，实际 ${jsInputField.fieldType}`
    );

    test(
      '4.5 input 字段为非必填',
      jsInputField.isRequired === false,
      `期望 false，实际 ${jsInputField.isRequired}`
    );

    test(
      '4.6 input 字段有描述',
      jsInputField.description !== undefined && jsInputField.description.length > 0,
      'input 字段缺少描述'
    );
  }

  // 5. 输出字段测试
  const jsOutputFields = jsTemplate.defaultData.outputFields || [];

  test(
    '5.1 输出字段存在',
    jsOutputFields.length > 0,
    '输出字段为空'
  );

  test(
    '5.2 输出字段数量正确',
    jsOutputFields.length === 1,
    `期望 1 个输出字段，实际 ${jsOutputFields.length} 个`
  );

  // 测试 output 字段
  const jsOutputField = jsOutputFields.find(f => f.fieldName === 'output');
  test(
    '5.3 output 字段存在',
    jsOutputField !== undefined,
    'output 字段未找到'
  );

  if (jsOutputField) {
    test(
      '5.4 output 字段类型为 dynamic',
      jsOutputField.fieldType === FieldType.Dynamic,
      `期望 ${FieldType.Dynamic}，实际 ${jsOutputField.fieldType}`
    );

    test(
      '5.5 output 字段为非必填',
      jsOutputField.isRequired === false,
      `期望 false，实际 ${jsOutputField.isRequired}`
    );

    test(
      '5.6 output 字段有描述',
      jsOutputField.description !== undefined && jsOutputField.description.length > 0,
      'output 字段缺少描述'
    );
  }
}

// ==================== 测试节点模板数组 ====================
console.log('\n【测试节点模板数组】');

const dataNodes = nodeTemplates.filter(t => t.category === NodeCategory.Data);

test(
  '6.1 数据处理分类节点数量正确',
  dataNodes.length === 2,
  `期望 2 个数据处理节点，实际 ${dataNodes.length} 个`
);

test(
  '6.2 DataProcess 节点在模板数组中',
  nodeTemplates.some(t => t.type === NodeType.DataProcess),
  'DataProcess 节点不在模板数组中'
);

test(
  '6.3 JavaScript 节点在模板数组中',
  nodeTemplates.some(t => t.type === NodeType.JavaScript),
  'JavaScript 节点不在模板数组中'
);

// ==================== 测试设计规范 ====================
console.log('\n【测试设计规范】');

if (dataProcessTemplate) {
  test(
    '7.1 DataProcess 节点颜色符合数据处理主题',
    dataProcessTemplate.color === '#2f54eb',
    'DataProcess 颜色应为蓝色主题 #2f54eb'
  );

  test(
    '7.2 DataProcess 节点图标为 emoji',
    /[\u{1F300}-\u{1F9FF}]|[⚙️]/u.test(dataProcessTemplate.icon),
    'DataProcess 图标应为 emoji 字符'
  );

  const dpInputFields = dataProcessTemplate.defaultData.inputFields || [];
  const dpOutputFields = dataProcessTemplate.defaultData.outputFields || [];
  test(
    '7.3 DataProcess 所有字段都有描述',
    [...dpInputFields, ...dpOutputFields].every(f => f.description && f.description.length > 0),
    'DataProcess 存在字段缺少描述'
  );
}

if (jsTemplate) {
  test(
    '7.4 JavaScript 节点颜色符合代码执行主题',
    jsTemplate.color === '#f5222d',
    'JavaScript 颜色应为红色主题 #f5222d'
  );

  test(
    '7.5 JavaScript 节点图标为 emoji',
    /[\u{1F300}-\u{1F9FF}]/u.test(jsTemplate.icon),
    'JavaScript 图标应为 emoji 字符'
  );

  const jsInputFields = jsTemplate.defaultData.inputFields || [];
  const jsOutputFields = jsTemplate.defaultData.outputFields || [];
  test(
    '7.6 JavaScript 所有字段都有描述',
    [...jsInputFields, ...jsOutputFields].every(f => f.description && f.description.length > 0),
    'JavaScript 存在字段缺少描述'
  );
}

// ==================== 测试功能性 ====================
console.log('\n【测试功能性】');

if (dataProcessTemplate) {
  const dpInputFields = dataProcessTemplate.defaultData.inputFields || [];
  const dpOutputFields = dataProcessTemplate.defaultData.outputFields || [];
  
  test(
    '8.1 DataProcess 支持数据输入输出',
    dpInputFields.some(f => f.fieldName === 'input') && 
    dpOutputFields.some(f => f.fieldName === 'output'),
    'DataProcess 节点应有 input 输入和 output 输出'
  );

  test(
    '8.2 DataProcess 输入字段为必填',
    dpInputFields.find(f => f.fieldName === 'input')?.isRequired === true,
    'DataProcess input 应为必填字段'
  );

  test(
    '8.3 DataProcess 使用 dynamic 类型支持灵活数据',
    dpInputFields.find(f => f.fieldName === 'input')?.fieldType === FieldType.Dynamic &&
    dpOutputFields.find(f => f.fieldName === 'output')?.fieldType === FieldType.Dynamic,
    'DataProcess 应使用 dynamic 类型支持各种数据类型'
  );
}

if (jsTemplate) {
  const jsInputFields = jsTemplate.defaultData.inputFields || [];
  const jsOutputFields = jsTemplate.defaultData.outputFields || [];
  
  test(
    '8.4 JavaScript 支持代码输入输出',
    jsInputFields.some(f => f.fieldName === 'input') && 
    jsOutputFields.some(f => f.fieldName === 'output'),
    'JavaScript 节点应有 input 输入和 output 输出'
  );

  test(
    '8.5 JavaScript 输入字段为可选',
    jsInputFields.find(f => f.fieldName === 'input')?.isRequired === false,
    'JavaScript input 应为可选字段以支持无参数代码'
  );

  test(
    '8.6 JavaScript 包含默认代码模板',
    jsTemplate.defaultData.content !== undefined && jsTemplate.defaultData.content.includes('return'),
    'JavaScript 节点应包含默认代码模板'
  );

  test(
    '8.7 JavaScript 使用 dynamic 类型支持灵活数据',
    jsInputFields.find(f => f.fieldName === 'input')?.fieldType === FieldType.Dynamic &&
    jsOutputFields.find(f => f.fieldName === 'output')?.fieldType === FieldType.Dynamic,
    'JavaScript 应使用 dynamic 类型支持各种数据类型'
  );
}

// ==================== 测试总结 ====================
console.log('\n' + '='.repeat(60));
console.log('\n【测试总结】');
console.log(`通过: ${passed}`);
console.log(`失败: ${failed}`);

if (failed === 0) {
  console.log('\n✓ 所有测试通过！任务 1.4 已完成。');
  console.log('\n数据处理节点特性：');
  
  if (dataProcessTemplate) {
    console.log('\n  DataProcess 节点：');
    console.log('    - 节点类型: dataProcess');
    console.log('    - 节点名称: 数据处理');
    console.log('    - 节点图标: ⚙️');
    console.log('    - 节点颜色: #2f54eb (蓝色)');
    console.log('    - 节点分类: 数据处理');
    console.log('    - 输入字段: input (必填, dynamic)');
    console.log('    - 输出字段: output (可选, dynamic)');
    console.log('    - 功能描述: 处理和转换数据');
  }
  
  if (jsTemplate) {
    console.log('\n  JavaScript 节点：');
    console.log('    - 节点类型: javaScript');
    console.log('    - 节点名称: JavaScript');
    console.log('    - 节点图标: 📜');
    console.log('    - 节点颜色: #f5222d (红色)');
    console.log('    - 节点分类: 数据处理');
    console.log('    - 输入字段: input (可选, dynamic)');
    console.log('    - 输出字段: output (可选, dynamic)');
    console.log('    - 默认代码: // 编写 JavaScript 代码\\nreturn input;');
    console.log('    - 功能描述: 执行 JavaScript 代码');
  }
  
  process.exit(0);
} else {
  console.log('\n✗ 存在失败的测试，请检查实现。');
  process.exit(1);
}

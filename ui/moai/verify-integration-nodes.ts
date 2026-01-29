/**
 * 验证任务 1.5：定义集成节点模板
 * 
 * 测试 Plugin 和 Wiki 节点的完整性和正确性
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

console.log('开始验证任务 1.5：定义集成节点模板\n');
console.log('='.repeat(60));

// ==================== 测试 Plugin 节点 ====================
console.log('\n【测试 Plugin 节点】');

const pluginTemplate = getNodeTemplate(NodeType.Plugin);

// 1. 节点存在性测试
test(
  '1.1 Plugin 节点模板存在',
  pluginTemplate !== undefined,
  'Plugin 节点模板未找到'
);

if (!pluginTemplate) {
  console.log('\n❌ Plugin 节点模板不存在，跳过该节点测试');
} else {
  // 2. 基础属性测试
  test(
    '2.1 节点类型正确',
    pluginTemplate.type === NodeType.Plugin,
    `期望 ${NodeType.Plugin}，实际 ${pluginTemplate.type}`
  );

  test(
    '2.2 节点名称正确',
    pluginTemplate.name === '插件调用',
    `期望 "插件调用"，实际 "${pluginTemplate.name}"`
  );

  test(
    '2.3 节点描述正确',
    pluginTemplate.description === '调用已配置的插件',
    `期望 "调用已配置的插件"，实际 "${pluginTemplate.description}"`
  );

  test(
    '2.4 节点图标正确',
    pluginTemplate.icon === '🔌',
    `期望 "🔌"，实际 "${pluginTemplate.icon}"`
  );

  test(
    '2.5 节点颜色正确',
    pluginTemplate.color === '#eb2f96',
    `期望 "#eb2f96"，实际 "${pluginTemplate.color}"`
  );

  test(
    '2.6 节点分类为集成',
    pluginTemplate.category === NodeCategory.Integration,
    `期望 ${NodeCategory.Integration}，实际 ${pluginTemplate.category}`
  );

  // 3. 默认数据测试
  test(
    '3.1 默认数据存在',
    pluginTemplate.defaultData !== undefined,
    '默认数据未定义'
  );

  test(
    '3.2 默认标题正确',
    pluginTemplate.defaultData.title === '插件调用',
    `期望 "插件调用"，实际 "${pluginTemplate.defaultData.title}"`
  );

  // 4. 输入字段测试
  const inputFields = pluginTemplate.defaultData.inputFields || [];

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

  // 测试 params 字段
  const paramsField = inputFields.find(f => f.fieldName === 'params');
  test(
    '4.3 params 字段存在',
    paramsField !== undefined,
    'params 字段未找到'
  );

  if (paramsField) {
    test(
      '4.4 params 字段类型为 object',
      paramsField.fieldType === FieldType.Object,
      `期望 ${FieldType.Object}，实际 ${paramsField.fieldType}`
    );

    test(
      '4.5 params 字段为非必填',
      paramsField.isRequired === false,
      `期望 false，实际 ${paramsField.isRequired}`
    );

    test(
      '4.6 params 字段有描述',
      paramsField.description !== undefined && paramsField.description.length > 0,
      'params 字段缺少描述'
    );
  }

  // 5. 输出字段测试
  const outputFields = pluginTemplate.defaultData.outputFields || [];

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

  // 测试 result 字段
  const resultField = outputFields.find(f => f.fieldName === 'result');
  test(
    '5.3 result 字段存在',
    resultField !== undefined,
    'result 字段未找到'
  );

  if (resultField) {
    test(
      '5.4 result 字段类型为 dynamic',
      resultField.fieldType === FieldType.Dynamic,
      `期望 ${FieldType.Dynamic}，实际 ${resultField.fieldType}`
    );

    test(
      '5.5 result 字段为非必填',
      resultField.isRequired === false,
      `期望 false，实际 ${resultField.isRequired}`
    );

    test(
      '5.6 result 字段有描述',
      resultField.description !== undefined && resultField.description.length > 0,
      'result 字段缺少描述'
    );
  }
}

// ==================== 测试 Wiki 节点 ====================
console.log('\n【测试 Wiki 节点】');

const wikiTemplate = getNodeTemplate(NodeType.Wiki);

// 1. 节点存在性测试
test(
  '1.1 Wiki 节点模板存在',
  wikiTemplate !== undefined,
  'Wiki 节点模板未找到'
);

if (!wikiTemplate) {
  console.log('\n❌ Wiki 节点模板不存在，跳过该节点测试');
} else {
  // 2. 基础属性测试
  test(
    '2.1 节点类型正确',
    wikiTemplate.type === NodeType.Wiki,
    `期望 ${NodeType.Wiki}，实际 ${wikiTemplate.type}`
  );

  test(
    '2.2 节点名称正确',
    wikiTemplate.name === '知识库查询',
    `期望 "知识库查询"，实际 "${wikiTemplate.name}"`
  );

  test(
    '2.3 节点描述正确',
    wikiTemplate.description === '从知识库中检索信息',
    `期望 "从知识库中检索信息"，实际 "${wikiTemplate.description}"`
  );

  test(
    '2.4 节点图标正确',
    wikiTemplate.icon === '📚',
    `期望 "📚"，实际 "${wikiTemplate.icon}"`
  );

  test(
    '2.5 节点颜色正确',
    wikiTemplate.color === '#52c41a',
    `期望 "#52c41a"，实际 "${wikiTemplate.color}"`
  );

  test(
    '2.6 节点分类为集成',
    wikiTemplate.category === NodeCategory.Integration,
    `期望 ${NodeCategory.Integration}，实际 ${wikiTemplate.category}`
  );

  // 3. 默认数据测试
  test(
    '3.1 默认数据存在',
    wikiTemplate.defaultData !== undefined,
    '默认数据未定义'
  );

  test(
    '3.2 默认标题正确',
    wikiTemplate.defaultData.title === '知识库查询',
    `期望 "知识库查询"，实际 "${wikiTemplate.defaultData.title}"`
  );

  // 4. 输入字段测试
  const wikiInputFields = wikiTemplate.defaultData.inputFields || [];

  test(
    '4.1 输入字段存在',
    wikiInputFields.length > 0,
    '输入字段为空'
  );

  test(
    '4.2 输入字段数量正确',
    wikiInputFields.length === 1,
    `期望 1 个输入字段，实际 ${wikiInputFields.length} 个`
  );

  // 测试 query 字段
  const queryField = wikiInputFields.find(f => f.fieldName === 'query');
  test(
    '4.3 query 字段存在',
    queryField !== undefined,
    'query 字段未找到'
  );

  if (queryField) {
    test(
      '4.4 query 字段类型为 string',
      queryField.fieldType === FieldType.String,
      `期望 ${FieldType.String}，实际 ${queryField.fieldType}`
    );

    test(
      '4.5 query 字段为必填',
      queryField.isRequired === true,
      `期望 true，实际 ${queryField.isRequired}`
    );

    test(
      '4.6 query 字段有描述',
      queryField.description !== undefined && queryField.description.length > 0,
      'query 字段缺少描述'
    );
  }

  // 5. 输出字段测试
  const wikiOutputFields = wikiTemplate.defaultData.outputFields || [];

  test(
    '5.1 输出字段存在',
    wikiOutputFields.length > 0,
    '输出字段为空'
  );

  test(
    '5.2 输出字段数量正确',
    wikiOutputFields.length === 1,
    `期望 1 个输出字段，实际 ${wikiOutputFields.length} 个`
  );

  // 测试 documents 字段
  const documentsField = wikiOutputFields.find(f => f.fieldName === 'documents');
  test(
    '5.3 documents 字段存在',
    documentsField !== undefined,
    'documents 字段未找到'
  );

  if (documentsField) {
    test(
      '5.4 documents 字段类型为 array',
      documentsField.fieldType === FieldType.Array,
      `期望 ${FieldType.Array}，实际 ${documentsField.fieldType}`
    );

    test(
      '5.5 documents 字段为非必填',
      documentsField.isRequired === false,
      `期望 false，实际 ${documentsField.isRequired}`
    );

    test(
      '5.6 documents 字段有描述',
      documentsField.description !== undefined && documentsField.description.length > 0,
      'documents 字段缺少描述'
    );
  }
}

// ==================== 测试节点模板数组 ====================
console.log('\n【测试节点模板数组】');

const integrationNodes = nodeTemplates.filter(t => t.category === NodeCategory.Integration);

test(
  '6.1 集成分类节点数量正确',
  integrationNodes.length === 2,
  `期望 2 个集成节点，实际 ${integrationNodes.length} 个`
);

test(
  '6.2 Plugin 节点在模板数组中',
  nodeTemplates.some(t => t.type === NodeType.Plugin),
  'Plugin 节点不在模板数组中'
);

test(
  '6.3 Wiki 节点在模板数组中',
  nodeTemplates.some(t => t.type === NodeType.Wiki),
  'Wiki 节点不在模板数组中'
);

// ==================== 测试设计规范 ====================
console.log('\n【测试设计规范】');

if (pluginTemplate) {
  test(
    '7.1 Plugin 节点颜色符合集成主题',
    pluginTemplate.color === '#eb2f96',
    'Plugin 颜色应为粉色主题 #eb2f96'
  );

  test(
    '7.2 Plugin 节点图标为 emoji',
    /[\u{1F300}-\u{1F9FF}]|[🔌]/u.test(pluginTemplate.icon),
    'Plugin 图标应为 emoji 字符'
  );

  const pluginInputFields = pluginTemplate.defaultData.inputFields || [];
  const pluginOutputFields = pluginTemplate.defaultData.outputFields || [];
  test(
    '7.3 Plugin 所有字段都有描述',
    [...pluginInputFields, ...pluginOutputFields].every(f => f.description && f.description.length > 0),
    'Plugin 存在字段缺少描述'
  );
}

if (wikiTemplate) {
  test(
    '7.4 Wiki 节点颜色符合知识库主题',
    wikiTemplate.color === '#52c41a',
    'Wiki 颜色应为绿色主题 #52c41a'
  );

  test(
    '7.5 Wiki 节点图标为 emoji',
    /[\u{1F300}-\u{1F9FF}]/u.test(wikiTemplate.icon),
    'Wiki 图标应为 emoji 字符'
  );

  const wikiInputFields = wikiTemplate.defaultData.inputFields || [];
  const wikiOutputFields = wikiTemplate.defaultData.outputFields || [];
  test(
    '7.6 Wiki 所有字段都有描述',
    [...wikiInputFields, ...wikiOutputFields].every(f => f.description && f.description.length > 0),
    'Wiki 存在字段缺少描述'
  );
}

// ==================== 测试功能性 ====================
console.log('\n【测试功能性】');

if (pluginTemplate) {
  const pluginInputFields = pluginTemplate.defaultData.inputFields || [];
  const pluginOutputFields = pluginTemplate.defaultData.outputFields || [];
  
  test(
    '8.1 Plugin 支持参数输入和结果输出',
    pluginInputFields.some(f => f.fieldName === 'params') && 
    pluginOutputFields.some(f => f.fieldName === 'result'),
    'Plugin 节点应有 params 输入和 result 输出'
  );

  test(
    '8.2 Plugin 参数字段为可选',
    pluginInputFields.find(f => f.fieldName === 'params')?.isRequired === false,
    'Plugin params 应为可选字段以支持无参数插件'
  );

  test(
    '8.3 Plugin 使用 object 类型支持结构化参数',
    pluginInputFields.find(f => f.fieldName === 'params')?.fieldType === FieldType.Object,
    'Plugin 应使用 object 类型支持结构化参数'
  );

  test(
    '8.4 Plugin 使用 dynamic 类型支持灵活返回值',
    pluginOutputFields.find(f => f.fieldName === 'result')?.fieldType === FieldType.Dynamic,
    'Plugin 应使用 dynamic 类型支持各种返回值类型'
  );
}

if (wikiTemplate) {
  const wikiInputFields = wikiTemplate.defaultData.inputFields || [];
  const wikiOutputFields = wikiTemplate.defaultData.outputFields || [];
  
  test(
    '8.5 Wiki 支持查询输入和文档输出',
    wikiInputFields.some(f => f.fieldName === 'query') && 
    wikiOutputFields.some(f => f.fieldName === 'documents'),
    'Wiki 节点应有 query 输入和 documents 输出'
  );

  test(
    '8.6 Wiki 查询字段为必填',
    wikiInputFields.find(f => f.fieldName === 'query')?.isRequired === true,
    'Wiki query 应为必填字段以确保查询有效'
  );

  test(
    '8.7 Wiki 使用 string 类型支持文本查询',
    wikiInputFields.find(f => f.fieldName === 'query')?.fieldType === FieldType.String,
    'Wiki 应使用 string 类型支持文本查询'
  );

  test(
    '8.8 Wiki 使用 array 类型返回文档列表',
    wikiOutputFields.find(f => f.fieldName === 'documents')?.fieldType === FieldType.Array,
    'Wiki 应使用 array 类型返回文档列表'
  );
}

// ==================== 测试总结 ====================
console.log('\n' + '='.repeat(60));
console.log('\n【测试总结】');
console.log(`通过: ${passed}`);
console.log(`失败: ${failed}`);

if (failed === 0) {
  console.log('\n✓ 所有测试通过！任务 1.5 已完成。');
  console.log('\n集成节点特性：');
  
  if (pluginTemplate) {
    console.log('\n  Plugin 节点：');
    console.log('    - 节点类型: plugin');
    console.log('    - 节点名称: 插件调用');
    console.log('    - 节点图标: 🔌');
    console.log('    - 节点颜色: #eb2f96 (粉色)');
    console.log('    - 节点分类: 集成');
    console.log('    - 输入字段: params (可选, object)');
    console.log('    - 输出字段: result (可选, dynamic)');
    console.log('    - 功能描述: 调用已配置的插件');
  }
  
  if (wikiTemplate) {
    console.log('\n  Wiki 节点：');
    console.log('    - 节点类型: wiki');
    console.log('    - 节点名称: 知识库查询');
    console.log('    - 节点图标: 📚');
    console.log('    - 节点颜色: #52c41a (绿色)');
    console.log('    - 节点分类: 集成');
    console.log('    - 输入字段: query (必填, string)');
    console.log('    - 输出字段: documents (可选, array)');
    console.log('    - 功能描述: 从知识库中检索信息');
  }
  
  process.exit(0);
} else {
  console.log('\n✗ 存在失败的测试，请检查实现。');
  process.exit(1);
}

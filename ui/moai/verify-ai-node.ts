/**
 * 验证任务 1.3：定义 AI 节点模板
 * 
 * 测试 AiChat 节点的完整性和正确性
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

console.log('开始验证任务 1.3：定义 AI 节点模板\n');
console.log('='.repeat(60));

// ==================== 测试 AiChat 节点 ====================
console.log('\n【测试 AiChat 节点】');

const aiChatTemplate = getNodeTemplate(NodeType.AiChat);

// 1. 节点存在性测试
test(
  '1.1 AiChat 节点模板存在',
  aiChatTemplate !== undefined,
  'AiChat 节点模板未找到'
);

if (!aiChatTemplate) {
  console.log('\n❌ AiChat 节点模板不存在，无法继续测试');
  process.exit(1);
}

// 2. 基础属性测试
test(
  '2.1 节点类型正确',
  aiChatTemplate.type === NodeType.AiChat,
  `期望 ${NodeType.AiChat}，实际 ${aiChatTemplate.type}`
);

test(
  '2.2 节点名称正确',
  aiChatTemplate.name === 'AI 对话',
  `期望 "AI 对话"，实际 "${aiChatTemplate.name}"`
);

test(
  '2.3 节点描述正确',
  aiChatTemplate.description === '调用 AI 模型进行对话',
  `期望 "调用 AI 模型进行对话"，实际 "${aiChatTemplate.description}"`
);

test(
  '2.4 节点图标正确',
  aiChatTemplate.icon === '🤖',
  `期望 "🤖"，实际 "${aiChatTemplate.icon}"`
);

test(
  '2.5 节点颜色正确',
  aiChatTemplate.color === '#1677ff',
  `期望 "#1677ff"，实际 "${aiChatTemplate.color}"`
);

test(
  '2.6 节点分类为 AI',
  aiChatTemplate.category === NodeCategory.AI,
  `期望 ${NodeCategory.AI}，实际 ${aiChatTemplate.category}`
);

// 3. 默认数据测试
test(
  '3.1 默认数据存在',
  aiChatTemplate.defaultData !== undefined,
  '默认数据未定义'
);

test(
  '3.2 默认标题正确',
  aiChatTemplate.defaultData.title === 'AI 对话',
  `期望 "AI 对话"，实际 "${aiChatTemplate.defaultData.title}"`
);

// 4. 输入字段测试
const inputFields = aiChatTemplate.defaultData.inputFields || [];

test(
  '4.1 输入字段存在',
  inputFields.length > 0,
  '输入字段为空'
);

test(
  '4.2 输入字段数量正确',
  inputFields.length === 2,
  `期望 2 个输入字段，实际 ${inputFields.length} 个`
);

// 测试 prompt 字段
const promptField = inputFields.find(f => f.fieldName === 'prompt');
test(
  '4.3 prompt 字段存在',
  promptField !== undefined,
  'prompt 字段未找到'
);

if (promptField) {
  test(
    '4.4 prompt 字段类型为 string',
    promptField.fieldType === FieldType.String,
    `期望 ${FieldType.String}，实际 ${promptField.fieldType}`
  );

  test(
    '4.5 prompt 字段为必填',
    promptField.isRequired === true,
    `期望 true，实际 ${promptField.isRequired}`
  );

  test(
    '4.6 prompt 字段有描述',
    promptField.description !== undefined && promptField.description.length > 0,
    'prompt 字段缺少描述'
  );
}

// 测试 context 字段
const contextField = inputFields.find(f => f.fieldName === 'context');
test(
  '4.7 context 字段存在',
  contextField !== undefined,
  'context 字段未找到'
);

if (contextField) {
  test(
    '4.8 context 字段类型为 string',
    contextField.fieldType === FieldType.String,
    `期望 ${FieldType.String}，实际 ${contextField.fieldType}`
  );

  test(
    '4.9 context 字段为非必填',
    contextField.isRequired === false,
    `期望 false，实际 ${contextField.isRequired}`
  );

  test(
    '4.10 context 字段有描述',
    contextField.description !== undefined && contextField.description.length > 0,
    'context 字段缺少描述'
  );
}

// 5. 输出字段测试
const outputFields = aiChatTemplate.defaultData.outputFields || [];

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

// 测试 response 字段
const responseField = outputFields.find(f => f.fieldName === 'response');
test(
  '5.3 response 字段存在',
  responseField !== undefined,
  'response 字段未找到'
);

if (responseField) {
  test(
    '5.4 response 字段类型为 string',
    responseField.fieldType === FieldType.String,
    `期望 ${FieldType.String}，实际 ${responseField.fieldType}`
  );

  test(
    '5.5 response 字段为非必填',
    responseField.isRequired === false,
    `期望 false，实际 ${responseField.isRequired}`
  );

  test(
    '5.6 response 字段有描述',
    responseField.description !== undefined && responseField.description.length > 0,
    'response 字段缺少描述'
  );
}

// 6. 节点模板数组测试
console.log('\n【测试节点模板数组】');

const aiNodes = nodeTemplates.filter(t => t.category === NodeCategory.AI);

test(
  '6.1 AI 分类节点数量正确',
  aiNodes.length === 1,
  `期望 1 个 AI 节点，实际 ${aiNodes.length} 个`
);

test(
  '6.2 AiChat 节点在模板数组中',
  nodeTemplates.some(t => t.type === NodeType.AiChat),
  'AiChat 节点不在模板数组中'
);

// 7. 设计规范测试
console.log('\n【测试设计规范】');

test(
  '7.1 节点颜色符合 AI 节点主题',
  aiChatTemplate.color === '#1677ff',
  '颜色应为蓝色主题 #1677ff'
);

test(
  '7.2 节点图标为 emoji',
  /[\u{1F300}-\u{1F9FF}]/u.test(aiChatTemplate.icon),
  '图标应为 emoji 字符'
);

test(
  '7.3 所有字段都有描述',
  [...inputFields, ...outputFields].every(f => f.description && f.description.length > 0),
  '存在字段缺少描述'
);

// 8. 功能性测试
console.log('\n【测试功能性】');

test(
  '8.1 支持基本的 AI 对话功能',
  inputFields.some(f => f.fieldName === 'prompt') && 
  outputFields.some(f => f.fieldName === 'response'),
  'AI 对话节点应有 prompt 输入和 response 输出'
);

test(
  '8.2 支持上下文传递',
  inputFields.some(f => f.fieldName === 'context'),
  'AI 对话节点应支持 context 输入'
);

test(
  '8.3 prompt 为必填字段',
  promptField?.isRequired === true,
  'prompt 应为必填字段以确保 AI 对话有效'
);

test(
  '8.4 context 为可选字段',
  contextField?.isRequired === false,
  'context 应为可选字段以支持灵活使用'
);

// ==================== 测试总结 ====================
console.log('\n' + '='.repeat(60));
console.log('\n【测试总结】');
console.log(`通过: ${passed}`);
console.log(`失败: ${failed}`);

if (failed === 0) {
  console.log('\n✓ 所有测试通过！任务 1.3 已完成。');
  console.log('\nAiChat 节点特性：');
  console.log('  - 节点类型: aiChat');
  console.log('  - 节点名称: AI 对话');
  console.log('  - 节点图标: 🤖');
  console.log('  - 节点颜色: #1677ff (蓝色)');
  console.log('  - 节点分类: AI 节点');
  console.log('  - 输入字段: prompt (必填), context (可选)');
  console.log('  - 输出字段: response');
  console.log('  - 功能描述: 调用 AI 模型进行对话');
  process.exit(0);
} else {
  console.log('\n✗ 存在失败的测试，请检查实现。');
  process.exit(1);
}

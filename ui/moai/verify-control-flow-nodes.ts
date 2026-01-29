/**
 * 验证控制流节点模板定义
 * 
 * 此脚本验证任务 1.2 的实现：
 * - Start 节点
 * - End 节点
 * - Condition 节点
 * - Fork 节点
 * - ForEach 节点
 */

import { nodeTemplates, getNodeTemplate } from './src/components/team/apps/workflow/nodeTemplates';
import { NodeType, NodeCategory, FieldType } from './src/components/team/apps/workflow/types';

// 颜色代码
const GREEN = '\x1b[32m';
const RED = '\x1b[31m';
const YELLOW = '\x1b[33m';
const RESET = '\x1b[0m';

let passCount = 0;
let failCount = 0;

function assert(condition: boolean, message: string) {
  if (condition) {
    console.log(`${GREEN}✓${RESET} ${message}`);
    passCount++;
  } else {
    console.log(`${RED}✗${RESET} ${message}`);
    failCount++;
  }
}

function testControlFlowNode(
  nodeType: NodeType,
  expectedName: string,
  expectedDescription: string,
  expectedIcon: string,
  expectedColor: string,
  expectedInputFields?: Array<{ fieldName: string; fieldType: FieldType; isRequired: boolean }>,
  expectedOutputFields?: Array<{ fieldName: string; fieldType: FieldType; isRequired: boolean }>
) {
  console.log(`\n${YELLOW}测试 ${expectedName} 节点 (${nodeType})${RESET}`);
  
  const template = getNodeTemplate(nodeType);
  
  // 基础属性验证
  assert(template !== undefined, `节点模板存在`);
  if (!template) return;
  
  assert(template.type === nodeType, `节点类型正确: ${template.type}`);
  assert(template.name === expectedName, `节点名称正确: ${template.name}`);
  assert(template.description === expectedDescription, `节点描述正确: ${template.description}`);
  assert(template.icon === expectedIcon, `节点图标正确: ${template.icon}`);
  assert(template.color === expectedColor, `节点颜色正确: ${template.color}`);
  assert(template.category === NodeCategory.Control, `节点分类为控制流: ${template.category}`);
  
  // 默认数据验证
  assert(template.defaultData !== undefined, `默认数据存在`);
  assert(template.defaultData.title === expectedName, `默认标题正确: ${template.defaultData.title}`);
  
  // 输入字段验证
  if (expectedInputFields) {
    assert(
      template.defaultData.inputFields !== undefined,
      `输入字段定义存在`
    );
    
    if (template.defaultData.inputFields) {
      assert(
        template.defaultData.inputFields.length === expectedInputFields.length,
        `输入字段数量正确: ${template.defaultData.inputFields.length}`
      );
      
      expectedInputFields.forEach((expected, index) => {
        const actual = template.defaultData.inputFields![index];
        assert(
          actual.fieldName === expected.fieldName,
          `  输入字段 ${index + 1} 名称: ${actual.fieldName}`
        );
        assert(
          actual.fieldType === expected.fieldType,
          `  输入字段 ${index + 1} 类型: ${actual.fieldType}`
        );
        assert(
          actual.isRequired === expected.isRequired,
          `  输入字段 ${index + 1} 必填: ${actual.isRequired}`
        );
      });
    }
  } else {
    assert(
      !template.defaultData.inputFields || template.defaultData.inputFields.length === 0,
      `无输入字段`
    );
  }
  
  // 输出字段验证
  if (expectedOutputFields) {
    assert(
      template.defaultData.outputFields !== undefined,
      `输出字段定义存在`
    );
    
    if (template.defaultData.outputFields) {
      assert(
        template.defaultData.outputFields.length === expectedOutputFields.length,
        `输出字段数量正确: ${template.defaultData.outputFields.length}`
      );
      
      expectedOutputFields.forEach((expected, index) => {
        const actual = template.defaultData.outputFields![index];
        assert(
          actual.fieldName === expected.fieldName,
          `  输出字段 ${index + 1} 名称: ${actual.fieldName}`
        );
        assert(
          actual.fieldType === expected.fieldType,
          `  输出字段 ${index + 1} 类型: ${actual.fieldType}`
        );
        assert(
          actual.isRequired === expected.isRequired,
          `  输出字段 ${index + 1} 必填: ${actual.isRequired}`
        );
      });
    }
  } else {
    assert(
      !template.defaultData.outputFields || template.defaultData.outputFields.length === 0,
      `无输出字段`
    );
  }
}

console.log(`${YELLOW}========================================${RESET}`);
console.log(`${YELLOW}验证控制流节点模板定义 (任务 1.2)${RESET}`);
console.log(`${YELLOW}========================================${RESET}`);

// 测试 Start 节点
testControlFlowNode(
  NodeType.Start,
  '开始',
  '工作流的起始节点',
  '▶️',
  '#52c41a',
  undefined, // 无输入字段
  [{ fieldName: 'trigger', fieldType: FieldType.Object, isRequired: false }]
);

// 测试 End 节点
testControlFlowNode(
  NodeType.End,
  '结束',
  '工作流的结束节点',
  '⏹️',
  '#ff4d4f',
  [{ fieldName: 'result', fieldType: FieldType.Dynamic, isRequired: false }],
  undefined // 无输出字段
);

// 测试 Condition 节点
testControlFlowNode(
  NodeType.Condition,
  '条件判断',
  '根据条件分支执行',
  '◆',
  '#faad14',
  [{ fieldName: 'condition', fieldType: FieldType.Boolean, isRequired: true }],
  [
    { fieldName: 'true', fieldType: FieldType.Dynamic, isRequired: false },
    { fieldName: 'false', fieldType: FieldType.Dynamic, isRequired: false }
  ]
);

// 测试 Fork 节点
testControlFlowNode(
  NodeType.Fork,
  '并行分支',
  '同时执行多个分支',
  '⑂',
  '#722ed1',
  [{ fieldName: 'input', fieldType: FieldType.Dynamic, isRequired: false }],
  [{ fieldName: 'branches', fieldType: FieldType.Array, isRequired: false }]
);

// 测试 ForEach 节点
testControlFlowNode(
  NodeType.ForEach,
  '循环遍历',
  '遍历数组中的每个元素',
  '🔁',
  '#13c2c2',
  [{ fieldName: 'array', fieldType: FieldType.Array, isRequired: true }],
  [
    { fieldName: 'item', fieldType: FieldType.Dynamic, isRequired: false },
    { fieldName: 'index', fieldType: FieldType.Number, isRequired: false }
  ]
);

// 验证所有控制流节点都在 nodeTemplates 数组中
console.log(`\n${YELLOW}验证节点模板数组${RESET}`);
const controlFlowNodes = nodeTemplates.filter(t => t.category === NodeCategory.Control);
assert(controlFlowNodes.length === 5, `控制流节点数量正确: ${controlFlowNodes.length}`);

const expectedTypes = [NodeType.Start, NodeType.End, NodeType.Condition, NodeType.Fork, NodeType.ForEach];
expectedTypes.forEach(type => {
  assert(
    controlFlowNodes.some(n => n.type === type),
    `${type} 节点存在于模板数组中`
  );
});

// 总结
console.log(`\n${YELLOW}========================================${RESET}`);
console.log(`${YELLOW}测试总结${RESET}`);
console.log(`${YELLOW}========================================${RESET}`);
console.log(`${GREEN}通过: ${passCount}${RESET}`);
console.log(`${RED}失败: ${failCount}${RESET}`);

if (failCount === 0) {
  console.log(`\n${GREEN}✓ 所有测试通过！任务 1.2 已完成。${RESET}`);
  process.exit(0);
} else {
  console.log(`\n${RED}✗ 有 ${failCount} 个测试失败。${RESET}`);
  process.exit(1);
}

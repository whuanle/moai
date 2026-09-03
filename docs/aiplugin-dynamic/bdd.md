# 动态插件（DynamicPlugin）行为规格（BDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../aiplugin-static/bdd.md](../aiplugin-static/bdd.md) ｜ 证据：[local-dev/dynamic-plugin-e2e.mjs](../../local-dev/dynamic-plugin-e2e.mjs)
> 规范：[../DOC-STANDARD.md](../DOC-STANDARD.md)。设计论证见 SDD，本文只写场景。

## Feature: 动态插件实例列表（管理员）

### Scenario: 查询动态插件实例列表

- Given 我是管理员，且已创建动态插件实例「greet_cn」（模板 dynamic_greet）
- When 我请求查询 plugin kind = dynamic 的列表
- Then 返回「greet_cn」，含 templeteKey=dynamic_greet、config、paramsExample

@DYN-S1 @auto:e2e

### Scenario: 未创建实例时列表为空

- Given 我是管理员，且注册表存在动态模板 dynamic_greet 但未创建任何实例
- When 我请求查询 plugin kind = dynamic 的列表
- Then 返回空列表（无实例行）

@DYN-S2 @auto:e2e

## Feature: 创建/编辑动态插件实例（管理员）

### Scenario: 创建合法实例

- Given 我是管理员，且存在动态模板 dynamic_greet
- When 我提交{pluginKey:"greet_cn",templeteKey:"dynamic_greet",title:"中文问候",config:"{\"Prefix\":\"你好\"}"}
- Then 返回成功，且刷新列表可见「greet_cn」

@DYN-S3 @auto:e2e

### Scenario: 创建实例 key 与已有注册表 key 重复被拒

- Given 我是管理员，且注册表存在动态模板 dynamic_greet
- When 我提交{pluginKey:"dynamic_greet",templeteKey:"dynamic_greet",...}
- Then 返回 409，提示「实例 Key 已被使用」

@DYN-S4 @auto:e2e

### Scenario: 创建实例 key 与其它实例重复被拒

- Given 已存在实例「greet_cn」
- When 我再次提交{pluginKey:"greet_cn",...}
- Then 返回 409，提示「实例 Key 已被使用」

@DYN-S5 @auto:e2e

### Scenario: 实例 key 大小写/开头不合规被拒

- Given 我是管理员
- When 我提交{pluginKey:"GreetCn",...} 或 {pluginKey:"1greet",...}
- Then 返回 400，提示「实例 Key 只能是小写字母、数字和下划线，且不能以数字开头」

@DYN-S6 @auto:e2e

### Scenario: 指定不存在的模板被拒

- Given 我是管理员，且模板 no_such 不存在
- When 我提交{pluginKey:"x",templeteKey:"no_such",...}
- Then 返回 404，提示「动态插件模板不存在」

@DYN-S7 @auto:e2e

### Scenario: 编辑实例信息

- Given 已存在实例「greet_cn」
- When 我提交{pluginKey:"greet_cn",templeteKey:"dynamic_greet",title:"新标题",config:"{\"Prefix\":\"Hi\"}"}
- Then 返回成功，且刷新列表标题为新标题、config 更新

@DYN-S8 @auto:e2e

### Scenario: 编辑时不可修改实例 key

- Given 已存在实例「greet_cn」
- When 我提交{pluginKey:"greet_cn",...}（pluginKey 用于定位，不参与改名）
- Then 实例 key 保持「greet_cn」不变

@DYN-S9 @auto:e2e

## Feature: 运行动态插件实例（管理员）

### Scenario: 用实例 key 运行实例成功

- Given 已存在实例「greet_cn」且 config 合法
- When 我请求运行{key:"greet_cn",requestJson:"{\"Name\":\"MoAI\"}"}
- Then 返回 success=true，且 dataJson 含「你好 MoAI」

@DYN-S10 @auto:e2e

### Scenario: 运行不存在的实例 key 被拒

- Given 我是管理员
- When 我请求运行{key:"no_such_instance",requestJson:"{}"}
- Then 返回 404，提示「插件不存在」

@DYN-S11 @auto:e2e

## Feature: 删除动态插件实例（管理员）

### Scenario: 删除实例

- Given 已存在实例「greet_cn」
- When 我删除实例{greet_cn}
- Then 返回成功，且刷新列表不再出现「greet_cn」

@DYN-S12 @auto:e2e

### Scenario: 删除不存在的实例被拒

- Given 我是管理员
- When 我删除实例{no_such}
- Then 返回 404，提示「动态插件实例不存在」

@DYN-S13 @auto:e2e

## Feature: 权限

### Scenario: 非管理员访问被拒

- Given 我是普通成员
- When 我请求查询/创建/运行/删除动态插件
- Then 返回 403，提示「只有管理员可以管理插件」

@DYN-S14 @auto:e2e

# 流程引擎

这是一个 AI Workflow 设计，基于 MoAI 的 AI 模型管理、知识库、插件引擎等能力，实现的一个可靠、灵活的流程编排引擎。

前后端代码分离，本设计主要是后端。

核心代码分为三个部分：

* 节点信息定义：设计流程编排时拖曳节点，会显示这个节点需要的参数、功能、说明等信息
* 节点设计定义：设计节点时需要填写每个参数怎么赋值、节点配置、提取需要的信息等
* 执行引擎：执行流程，逐个处理节点并流式返回工作状态



## 节点信息定义

流程系统固定有多个节点类型，当在前端界面编排设计时，需要显示每个节点的信息，包括节点名称、节点描述、节点需要的参数字段、节点会输出什么数据，用户使用此节点时，配置输入参数需要符合要求。

节点信息定义相关的类型使用 Define 结尾。



系统中现有以下类型的节点：

```csharp
/// <summary>
/// 节点类型.
/// </summary>
public enum NodeType
{
    /// <summary>
    /// 未配置节点类型.
    /// </summary>
    [JsonPropertyName("none")]
    None,

    /// <summary>
    /// 开始节点.
    /// </summary>
    [JsonPropertyName("start")]
    Start,

    /// <summary>
    /// 结束节点.
    /// </summary>
    [JsonPropertyName("end")]
    End,

    /// <summary>
    /// 调用插件.
    /// </summary>
    [JsonPropertyName("plugin")]
    Plugin,

    /// <summary>
    /// 调用知识库.
    /// </summary>
    [JsonPropertyName("wiki")]
    Wiki,

    /// <summary>
    /// ai 对话.
    /// </summary>
    [JsonPropertyName("ai_chat")]
    AiChat,

    /// <summary>
    /// 条件判断.
    /// </summary>
    [JsonPropertyName("condition")]
    Condition,

    /// <summary>
    /// 针对数组参数逐个迭代.
    /// </summary>
    [JsonPropertyName("foreach")]
    ForEach,

    /// <summary>
    /// 条件判断分支.
    /// </summary>
    [JsonPropertyName("fork")]
    Fork,
    
    /// <summary>
    /// Javascript。
    /// </summary>
    [JsonPropertyName("javascript")]
    JavaScript,
    
    /// <summary>
    /// 数据处理器.
    /// </summary>
    [JsonPropertyName("data_process")]
    DataProcess,
}
```



每种节点类型的定义：

```csharp
/// <summary>
/// 节点定义，说明一个节点的信息和输入输出.
/// </summary>
public interface INodeDefine
{
    /// <summary>
    /// 节点类型.
    /// </summary>
    public NodeType NodeType { get; }

    /// <summary>
    /// 自定义名称.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// 可以接受的参数类型输入参数，第一层必须都是 intput.
    /// </summary>
    public FieldDefine Input { get; }

    /// <summary>
    /// 输出参数，第一层必须都是 output.
    /// </summary>
    public FieldDefine OutPut { get; }
}

/// <summary>
/// 字段定义.
/// </summary>
public class FieldDefine
{
    /// <summary>
    /// 字段名称.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// 此字段必须有赋值.
    /// </summary>
    public bool IsRequired { get; init; }

    /// <summary>
    /// 字段类型.
    /// </summary>
    public FieldTypeDefine FieldType { get; init; }

    /// <summary>
    /// 子层.
    /// </summary>
    public IReadOnlyCollection<FieldDefine> Children { get; init; } = Array.Empty<FieldDefine>();
}

/// <summary>
/// 字段类型定义，跟 json 类型一样.
/// </summary>
public enum FieldTypeDefine
{
    /// <summary>
    /// 没有结果.
    /// </summary>
    [JsonPropertyName("empty")]
    Empty,

    /// <summary>
    /// 字符串.
    /// </summary>
    [JsonPropertyName("string")]
    String,

    /// <summary>
    /// 数组.
    /// </summary>
    [JsonPropertyName("number")]
    Number,

    /// <summary>
    /// 布尔型.
    /// </summary>
    [JsonPropertyName("boolean")]
    Boolean,

    /// <summary>
    /// 对象.
    /// </summary>
    [JsonPropertyName("object")]
    Object,

    /// <summary>
    /// 数组.
    /// </summary>
    [JsonPropertyName("array")]
    Array,

    /// <summary>
    /// 任意类型.
    /// </summary>
    [JsonPropertyName("dynamic")]
    Dynamic
}

```



每个节点的 INodeDefine不一定会固定。

对于插件节点，插件使用的是 MoAI.Plugin 里面的插件引擎，有自定义插件、内置插件，每个插件的输入参数输出参数完全不一样。

例如在 FeishuWebHookTextPlugin 插件里面，请求参数只有字符串；PaddleocrPlugin 插件里面，参数是 PaddleOcrParams，所以还需要读取插件的一些信息才能生成 FieldDefine 。

所以对于开始节点等一开始就有对应的 INodeDefine 是固定的，但是对于插件引擎这种节点，还需要在用户选择使用哪个插件时，根据插件信息实时生成 INodeDefine。



## 节点设计定义

设计节点时，需要设计当前节点的每个输入字段怎么从上一个节点中提取数据出来，配置节点需要的参数、配置节点之间的连线，节点定义和连线数据需要存储到数据库，节点设计相关的代码使用 `Design` 结尾。



例如使用一个节点时，节点设计的对象如下：

```csharp
/// <summary>
/// 节点设计.
/// </summary>
public class NodeDesign
{
    /// <summary>
    /// 节点 key，整个流程唯一.
    /// </summary>
    public string NodeKey { get; init; }

    /// <summary>
    /// 自定义名称.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// 绑定的节点类型.
    /// </summary>
    public NodeType NodeType { get; }

    /// <summary>
    /// 指向下一个节点的 key.
    /// </summary>
    public string NextNodeKey { get; }

    /// <summary>
    /// 设计怎么接收输入参数.
    /// </summary>
    public IReadOnlyCollection<FieldDesign> Input { get; }
}
/// <summary>
/// 字段设计
/// </summary>
public class FieldDesign
{
    /// <summary>
    /// 对应节点类型中的字段名称.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 赋值表达式类型，也就是怎么提取值赋值.
    /// </summary>
    public FieldExpressionType ExpressionType { get; init; } = FieldExpressionType.Fixed;

    /// <summary>
    /// 表达式.
    /// </summary>
    public string Expression { get; init; } = string.Empty;
}
public enum FieldExpressionType
{
    /// <summary>
    /// 未设置或固定，常量值，即直接赋值.
    /// </summary>
    [JsonPropertyName("none")]
    Fixed,

    /// <summary>
    /// 使用变量赋值，支持系统变量、全局变量、节点变量等.  
    /// </summary>
    [JsonPropertyName("variable")]
    Variable,

    /// <summary>
    /// JsonPath 表达式.
    /// </summary>
    [JsonPropertyName("jsonpath")]
    JsonPath,

    /// <summary>
    /// 字符串插值表达式，可使用变量插值，不能对数组、对象类型使用.
    /// </summary>
    [JsonPropertyName("string_interpolation")]
    StringInterpolation
}
```



每个节点都有输入参数要求，例如某个节点需要填入多个参数，每个参数可以是 josn 各种类型。

Fixed 模式字段表达式：

```
a.a = "测试"
```

> 常量，直接用这个值。



Variable 模式的表达式：

```
a.a = sys.aaa

// 如果 aaa 是数组
a.b = sys.aaa[0].name

// 如果 c 是数组
a.c = sys.aaa[*]
```

> 从sys变量列表或 InputJsonMap 里面直接找出变量。



jsonpath 表达式：

```
// { "Name": "Acme Co", Products: [{ "Name": "Anvil", "Price": 50 }] }
b = "$.Manufacturers[?(@.Name == \"Acme Co\")]"
```

> 使用的是 Json.Path 框架。



string_interpolation 表达式：

```
c = "我的名字是:{sys.name}"
```

> 使用的是 SmartFormat.NET 框架，方便实现字符串插值.



## 执行引擎定义

每类节点都有执行器，每个 NodeType 枚举都有唯一的执行器。

抽象接口：

```csharp
/// <summary>
/// 节点运行器.
/// </summary>
public interface INodeRuntime
{
    public IAsyncEnumerable<WorkflowProcessingItem> RunAsync(IWorkflowContext workflowContext, INodePipeline pipeline, CancellationToken cancellationToken = default);
}
```



对于每类节点类型都要实现一个对应的 INodeRuntime，在流程启动后，引擎按顺序创建每个节点实例，如果调用 RunAsync 函数。



IWorkflowContext 定义了流程执行的上下文信息，以便记录执行情况和读取里面的节点信息，数据都是只读的。

每个流程执行信息并不需要写到 IWorkflowContext，因为流程引擎会自动记录。

```csharp

/// <summary>
/// 工作流上下文.
/// </summary>
public interface IWorkflowContext
{
    /// <summary>
    /// 实例 id.
    /// </summary>
    public Guid InstanceId { get; }

    /// <summary>
    /// 流程定义的 id.
    /// </summary>
    public Guid DefinitionId { get; }

    /// <summary>
    /// 流程工作状态.
    /// </summary>
    public WorkerState WorkflowState { get; }

    /// <summary>
    /// 运行参数，系统变量和自定义传入的变量.
    /// </summary>
    public IReadOnlyDictionary<string, object?> RuntimeParamters { get; }

    /// <summary>
    /// 启动时间.
    /// </summary>
    public DateTimeOffset StartTime { get; }

    /// <summary>
    /// 流式数据已执行的节点 Key 集合.
    /// </summary>
    public IReadOnlyCollection<string> ExecutedNodeKeys { get; }

    /// <summary>
    /// 当前已执行或正在执行的节点流水线集合.
    /// </summary>
    public IReadOnlyDictionary<string, INodePipeline> NodePipelines { get; }
}
```



但是节点一般只需要关注 INodePipeline，INodePipeline 是节点运行的依赖构建，节点需要依赖里面记录的信息完成任务。

```csharp

/// <summary>
/// 执行一个节点时的流水线，包括需要的数据和环境.
/// </summary>
public interface INodePipeline
{
    /// <summary>
    /// 当前节点的定义.
    /// </summary>
    public INodeDefine NodeDefine { get; }

    /// <summary>
    /// 节点工作状态.
    /// </summary>
    public WorkerState NodeState { get; }

    /// <summary>
    /// 输入数据，使用 InputJsonElement 形式表示.
    /// </summary>
    public JsonElement InputJsonElement { get; }

    /// <summary>
    /// 输入数据，使用已解析的结构表示,全部平铺了.
    /// </summary>
    public IReadOnlyDictionary<string, object?> InputJsonMap { get; }
}
```



流式返回时返回每个节点的执行信息和状态。

```csharp
/// <summary>
/// 流程编排流式数据.
/// </summary>
public class WorkflowProcessingItem
{
    /// <summary>
    /// 节点类型.
    /// </summary>
    public virtual NodeType NodeType { get; init; }

    /// <summary>
    /// 节点 key，唯一标识.
    /// </summary>
    public string NodeKey { get; init; } = string.Empty;

    /// <summary>
    /// 下一个节点的 Key.
    /// </summary>
    public string NextNodeKey { get; init; } = string.Empty;

    /// <summary>
    /// 输入数据，使用 JsonElement 形式表示.
    /// </summary>
    public JsonElement? Input { get; init; }

    /// <summary>
    /// 输出数据，使用 JsonElement 形式表示.
    /// </summary>
    public JsonElement? Output { get; init; }

    /// <summary>
    /// 节点工作状态.
    /// </summary>
    public WorkerState State { get; init; }

    // todo: 其它调用的插件等数据
}
```



执行引擎大概是这样设计的：

```csharp
public class WorkflowRuntime
{
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public WorkflowRuntime(IServiceScopeFactory serviceScopeFactory)
    {
        _serviceScopeFactory = serviceScopeFactory;
    }

    public async IAsyncEnumerable<WorkflowProcessingItem> Handler(WorkflowDesign workflowDesign, IReadOnlyDictionary<string, object?> runtimeParmaters, CancellationToken cancellationToken = default)
    {

        Dictionary<string, INodePipeline> nodePipelines = new();
        List<string> executedNodeKeys = new();
        Dictionary<string, object?> initialParameters = new();
        InitRuntimeParamters(workflowDefine, runtimeParmaters, initialParameters);

        WorkflowContext workflowContext = new()
        {
            DefinitionId = workflowDefine.DefinitionId,
            InstanceId = Guid.CreateVersion7(),
            NodePipelines = nodePipelines,
            RuntimeParamters = runtimeParmaters,
            ExecutedNodeKeys = executedNodeKeys,
        };

        var startNodePipeline = new NodePipeline
        {
            NodeDefine = workflowDefine.Nodes.FirstOrDefault(x => x.NodeType == NodeType.Start)!,
            InputJsonMap = initialParameters,
            NodeState = WorkerState.Processing,
        };

        nodePipelines["start"] = startNodePipeline;

        // todo: 存到数据库

        var nodeEnumerator = workflowDefine.Nodes.GetEnumerator();
        while (workflowContext.WorkflowState >= WorkerState.Cancal || cancellationToken.IsCancellationRequested || !nodeEnumerator.MoveNext())
        {
            var nodeDefine = nodeEnumerator.Current;
            var nodePipeline = workflowContext.NodePipelines[nodeDefine.Key];

            using var scope = _serviceScopeFactory.CreateScope();

            var nodeRuntime = scope.ServiceProvider.GetRequiredKeyedService<INodeRuntime>(nodeDefine.NodeType);
            
            WorkflowProcessingItem lastNodeResponse = null!;
            await foreach (var nodeResponse in nodeRuntime.RunAsync(workflowContext, nodePipeline, cancellationToken))
            {
                lastNodeResponse = nodeResponse;
                yield return nodeResponse;
            }

            // 整理上一个节点输出的数据
            var ouput = lastNodeResponse.Output;
             // 将 JsonElement 使用 JsonParseHelper.cs 生成字典铺平,并且把全局变量合并到里面
            output = ...

            // 为下一个节点的输入做准备
            nodePipelines[nodePipeline.NodeDefine.NextNodeKey] = new NodePipeline
            {
                NodeDefine = workflowDefine.Nodes.FirstOrDefault(x => x.Key == nodePipeline.NodeDefine.NextNodeKey)!,
                NodeState = WorkerState.Wait,
                InputJsonMap = ouput
            };
        }

        // 存数据库
        // 结束流式
    }

    private static void InitRuntimeParamters(WorkflowDefine workflowDefine, IReadOnlyDictionary<string, object?> runtimeParmaters, Dictionary<string, object?> initialParameters)
    {
        initialParameters["sys.define_id"] = workflowDefine.DefinitionId;
        initialParameters["sys.start_time"] = DateTimeOffset.Now;
        foreach (var item in runtimeParmaters)
        {
            initialParameters[item.Key] = item.Value;
        }
    }
}
```



只需要逐步处理节点，并拦截每个 nodeResponse，即可知道节点状态，所以没必要让节点读写 WorkflowContext 和 INodePipline。



## 细节要求

流程设计时，将布局数据和节点数据分开，布局数据只需要关联 nodeKey 即可。



实际上引擎对输出数据的处理是解析成扁平化数据。

例如：

```
{
"A1":{
  "Name" : "name"
 }
}

// "A1.Name" : "name"
```



直接使用变量很简单：`sys.define_id`、`A1.Name` 这样就可以读取值。

全局变量使用 `sys.` 开头读取，数量和名字是固定的；而启动参数需要在流程变量里面定义，启动流程数外部传递进去，也是使用 `sys.` 读取，这两种变量都是全局可以读取的。

正常变量可以通过 `input.{field}` 使用，如果是数组，可以使用 `intput.mya[*]` 使用全部或 `input.mya[0].a` 这样。如果 input 就是字符串，那么应该使用 `input` 就行。

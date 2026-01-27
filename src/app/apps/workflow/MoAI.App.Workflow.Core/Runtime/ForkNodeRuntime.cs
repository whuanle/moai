using MoAI.Workflow.Enums;
using MoAI.Workflow.Models;

namespace MoAI.Workflow.Runtime;

/// <summary>
/// Fork 节点运行时实现.
/// Fork 节点负责并行执行多个分支并等待所有分支完成后再继续.
/// </summary>
public class ForkNodeRuntime : INodeRuntime
{
    /// <inheritdoc/>
    public NodeType SupportedNodeType => NodeType.Fork;

    /// <summary>
    /// 执行 Fork 节点逻辑.
    /// 并行执行多个分支，等待所有分支完成，并收集所有分支的结果.
    /// </summary>
    /// <param name="nodeDefine">节点定义.</param>
    /// <param name="inputs">节点输入数据，应包含 branches 字段.</param>
    /// <param name="context">工作流上下文.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>包含所有分支执行结果的执行结果.</returns>
    public async Task<NodeExecutionResult> ExecuteAsync(
        INodeDefine nodeDefine,
        Dictionary<string, object> inputs,
        IWorkflowContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            // 1. 验证必需的输入字段
            if (!inputs.TryGetValue("branches", out var branchesObj))
            {
                return NodeExecutionResult.Failure("缺少必需的输入字段: branches");
            }

            // 2. 将输入转换为分支集合
            var branches = ConvertToBranches(branchesObj);
            if (branches == null || branches.Count == 0)
            {
                return NodeExecutionResult.Failure("branches 字段必须包含至少一个分支");
            }

            // 3. 并行执行所有分支
            var branchTasks = new List<Task<BranchResult>>();
            
            for (int i = 0; i < branches.Count; i++)
            {
                var branchIndex = i;
                var branch = branches[i];
                
                // 为每个分支创建异步任务
                var branchTask = Task.Run(async () =>
                {
                    try
                    {
                        // 检查取消令牌
                        if (cancellationToken.IsCancellationRequested)
                        {
                            return new BranchResult
                            {
                                Index = branchIndex,
                                BranchName = branch.Name,
                                Success = false,
                                Error = "分支执行被取消"
                            };
                        }

                        // 执行分支逻辑（这里只是标记分支已准备好执行）
                        // 实际的分支节点执行由工作流引擎处理
                        await Task.Delay(1, cancellationToken); // 模拟异步操作

                        return new BranchResult
                        {
                            Index = branchIndex,
                            BranchName = branch.Name,
                            NextNodeKey = branch.NextNodeKey,
                            Success = true,
                            Data = branch.Data
                        };
                    }
                    catch (OperationCanceledException)
                    {
                        return new BranchResult
                        {
                            Index = branchIndex,
                            BranchName = branch.Name,
                            Success = false,
                            Error = "分支执行被取消"
                        };
                    }
                    catch (Exception ex)
                    {
                        return new BranchResult
                        {
                            Index = branchIndex,
                            BranchName = branch.Name,
                            Success = false,
                            Error = $"分支执行失败: {ex.Message}"
                        };
                    }
                }, cancellationToken);

                branchTasks.Add(branchTask);
            }

            // 4. 等待所有分支完成
            BranchResult[] branchResults;
            try
            {
                branchResults = await Task.WhenAll(branchTasks);
            }
            catch (Exception ex)
            {
                return NodeExecutionResult.Failure($"等待分支完成时发生错误: {ex.Message}");
            }

            // 5. 检查是否有分支失败
            var failedBranches = branchResults.Where(r => !r.Success).ToList();
            if (failedBranches.Any())
            {
                var errorMessages = string.Join("; ", failedBranches.Select(b => $"{b.BranchName}: {b.Error}"));
                return NodeExecutionResult.Failure($"部分分支执行失败: {errorMessages}");
            }

            // 6. 构建输出
            var output = new Dictionary<string, object>
            {
                ["branches"] = branchResults.Select(r => new Dictionary<string, object>
                {
                    ["index"] = r.Index,
                    ["name"] = r.BranchName,
                    ["nextNodeKey"] = r.NextNodeKey ?? string.Empty,
                    ["data"] = r.Data ?? new Dictionary<string, object>()
                }).ToList(),
                ["branchCount"] = branchResults.Length,
                ["allSucceeded"] = branchResults.All(r => r.Success)
            };

            return NodeExecutionResult.Success(output);
        }
        catch (Exception ex)
        {
            return NodeExecutionResult.Failure(ex);
        }
    }

    /// <summary>
    /// 将对象转换为分支集合.
    /// </summary>
    /// <param name="obj">要转换的对象.</param>
    /// <returns>分支集合，如果无法转换则返回 null.</returns>
    private List<BranchInfo>? ConvertToBranches(object obj)
    {
        if (obj == null)
        {
            return null;
        }

        var branches = new List<BranchInfo>();

        // 如果是字符串，尝试解析为 JSON
        if (obj is string jsonStr)
        {
            try
            {
                var parsed = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, object>>>(jsonStr);
                if (parsed != null)
                {
                    foreach (var item in parsed)
                    {
                        branches.Add(ParseBranchInfo(item));
                    }
                    return branches;
                }
            }
            catch
            {
                // 如果解析失败，将字符串视为单个分支名称
                branches.Add(new BranchInfo { Name = jsonStr });
                return branches;
            }
        }

        // 如果是集合类型
        if (obj is System.Collections.IEnumerable enumerable and not string)
        {
            foreach (var item in enumerable)
            {
                if (item is Dictionary<string, object> dict)
                {
                    branches.Add(ParseBranchInfo(dict));
                }
                else if (item is string branchName)
                {
                    branches.Add(new BranchInfo { Name = branchName });
                }
                else
                {
                    // 尝试将对象转换为字典
                    var itemDict = ConvertObjectToDictionary(item);
                    if (itemDict != null)
                    {
                        branches.Add(ParseBranchInfo(itemDict));
                    }
                }
            }
            return branches;
        }

        // 如果是单个字典对象
        if (obj is Dictionary<string, object> singleDict)
        {
            branches.Add(ParseBranchInfo(singleDict));
            return branches;
        }

        return null;
    }

    /// <summary>
    /// 从字典解析分支信息.
    /// </summary>
    /// <param name="dict">包含分支信息的字典.</param>
    /// <returns>分支信息对象.</returns>
    private BranchInfo ParseBranchInfo(Dictionary<string, object> dict)
    {
        var branchInfo = new BranchInfo();

        if (dict.TryGetValue("name", out var name))
        {
            branchInfo.Name = name?.ToString() ?? string.Empty;
        }

        if (dict.TryGetValue("nextNodeKey", out var nextNodeKey))
        {
            branchInfo.NextNodeKey = nextNodeKey?.ToString();
        }

        if (dict.TryGetValue("data", out var data))
        {
            if (data is Dictionary<string, object> dataDict)
            {
                branchInfo.Data = dataDict;
            }
            else
            {
                branchInfo.Data = ConvertObjectToDictionary(data);
            }
        }

        return branchInfo;
    }

    /// <summary>
    /// 将对象转换为字典.
    /// </summary>
    /// <param name="obj">要转换的对象.</param>
    /// <returns>字典对象，如果无法转换则返回 null.</returns>
    private Dictionary<string, object>? ConvertObjectToDictionary(object obj)
    {
        if (obj == null)
        {
            return null;
        }

        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(obj);
            return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 分支信息类.
    /// </summary>
    private class BranchInfo
    {
        public string Name { get; set; } = string.Empty;
        public string? NextNodeKey { get; set; }
        public Dictionary<string, object>? Data { get; set; }
    }

    /// <summary>
    /// 分支执行结果类.
    /// </summary>
    private class BranchResult
    {
        public int Index { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public string? NextNodeKey { get; set; }
        public bool Success { get; set; }
        public string? Error { get; set; }
        public Dictionary<string, object>? Data { get; set; }
    }
}

using Maomi;
using MoAI.Workflow.Enums;
using MoAI.Workflow.Models;

namespace MoAI.Workflow.Runtime;

/// <summary>
/// Fork 节点运行时实现.
/// Fork 节点负责并行执行多个分支并等待所有分支完成后再继续.
/// </summary>
[InjectOnTransient(ServiceKey = NodeType.Fork)]
public class ForkNodeRuntime : INodeRuntime
{
    /// <inheritdoc/>
    public NodeType SupportedNodeType => NodeType.Fork;

    /// <summary>
    /// 执行 Fork 节点逻辑.
    /// </summary>
    public async Task<NodeExecutionResult> ExecuteAsync(
        Dictionary<string, object> inputs,
        INodePipeline pipeline,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!inputs.TryGetValue("branches", out var branchesObj))
            {
                return NodeExecutionResult.Failure("缺少必需的输入字段: branches");
            }

            var branches = ConvertToBranches(branchesObj);
            if (branches == null || branches.Count == 0)
            {
                return NodeExecutionResult.Failure("branches 字段必须包含至少一个分支");
            }

            var branchTasks = new List<Task<BranchResult>>();
            
            for (int i = 0; i < branches.Count; i++)
            {
                var branchIndex = i;
                var branch = branches[i];
                
                var branchTask = Task.Run(async () =>
                {
                    try
                    {
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

                        await Task.Delay(1, cancellationToken);

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

            BranchResult[] branchResults;
            try
            {
                branchResults = await Task.WhenAll(branchTasks);
            }
            catch (Exception ex)
            {
                return NodeExecutionResult.Failure($"等待分支完成时发生错误: {ex.Message}");
            }

            var failedBranches = branchResults.Where(r => !r.Success).ToList();
            if (failedBranches.Any())
            {
                var errorMessages = string.Join("; ", failedBranches.Select(b => $"{b.BranchName}: {b.Error}"));
                return NodeExecutionResult.Failure($"部分分支执行失败: {errorMessages}");
            }

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

    private List<BranchInfo>? ConvertToBranches(object obj)
    {
        if (obj == null)
        {
            return null;
        }

        var branches = new List<BranchInfo>();

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
                branches.Add(new BranchInfo { Name = jsonStr });
                return branches;
            }
        }

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
                    var itemDict = ConvertObjectToDictionary(item);
                    if (itemDict != null)
                    {
                        branches.Add(ParseBranchInfo(itemDict));
                    }
                }
            }
            return branches;
        }

        if (obj is Dictionary<string, object> singleDict)
        {
            branches.Add(ParseBranchInfo(singleDict));
            return branches;
        }

        return null;
    }

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

    private class BranchInfo
    {
        public string Name { get; set; } = string.Empty;
        public string? NextNodeKey { get; set; }
        public Dictionary<string, object>? Data { get; set; }
    }

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

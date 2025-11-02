using MoAI.Infra.Helpers;
using MoAI.Workflow.Models;
using Newtonsoft.Json.Linq;
using SmartFormat;
using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;

namespace MoAI.Workflow.Helpers;

public static class WorkflowValueHelper
{
    /// <summary>
    /// 解析字段表达式生成赋值输出，如果字段是对象或数组，会生成平铺的多层结构.
    /// </summary>
    /// <param name="inputJsonObject"></param>
    /// <param name="inputParamters"></param>
    /// <param name="field"></param>
    /// <returns></returns>
    public static IReadOnlyDictionary<string, object> ParseFieldValue(JObject inputJsonObject, IReadOnlyDictionary<string, object> inputParamters, WorkflowFieldDefinition field)
    {
        Dictionary<string, object> outputParamter = new Dictionary<string, object>();

        if (string.IsNullOrEmpty(field.Expression.ExpressionValue))
        {
            return outputParamter;
        }

        // 获取赋值规则
        var expression = field.Expression;

        // 如果是数组，说明没有下一层，直接生成赋值
        if (field.Type == WorkflowFieldType.Array || field.Type == WorkflowFieldType.Object)
        {
            if (expression.ExpressionType == WorkflowFieldExpressionType.Variable)
            {
                // 对方必须也是个数组或对象
                // Var3.Var4 = Var1.Var2
                // Var3.Var4[0].x.x = Var1.Var2[0].x.x
                // Var3.Var4[0].x.y = Var1.Var2[0].x.y
                foreach (var item in inputParamters.Where(p => p.Key.StartsWith(expression.ExpressionValue, StringComparison.CurrentCultureIgnoreCase)))
                {
                    outputParamter.Add(item.Key.Replace(expression.ExpressionValue, field.Key, StringComparison.CurrentCultureIgnoreCase), ConvertType(item.Value, field.ArrayChildrenType));
                }
            }
            else if (expression.ExpressionType == WorkflowFieldExpressionType.JsonPath)
            {
                foreach (var item in inputJsonObject.SelectTokens(expression.ExpressionValue))
                {
                    // item 可能是对象有可能是数组，需要进一步平铺
                    var itemObject = item.ToString();
                    var allTokens = JsonParseHelper.Read(new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(itemObject)), new System.Text.Json.JsonReaderOptions { MaxDepth = 3 });

                    // 平铺所有的 token
                    // {"Name": "Elbow Grease", "Price": 99.95}  => Name, Price
                    // [{"Name": "Elbow Grease", "Price": 99.95},...]   => [0].Name, [0].Price, [1].Name, [1].Price
                    foreach (var item1 in allTokens)
                    {
                        // "Var3.Var4" + "[0].Name" => "Var3.Var4[0].Name"
                        outputParamter.Add(field.Key + item1.Key, item1.Value);
                    }
                }
            }

            return outputParamter; // 如果没有值，则返回空数组
        }

        if (expression.ExpressionType == WorkflowFieldExpressionType.Variable)
        {
            // 简单变量表达式只需要将复制值给这个 key 即可
            if (inputParamters.TryGetValue(expression.ExpressionValue, out var readValue))
            {
                outputParamter.Add(field.Key, ConvertType(readValue, field.Type));
            }
        }
        else if (expression.ExpressionType == WorkflowFieldExpressionType.JsonPath)
        {
            var token = inputJsonObject.SelectToken(expression.ExpressionValue);
            if (token != null)
            {
                // 直接将 token 的值转换为对应的类型
                // todo: 存疑，调试时优化
                var value = token.ToObject<object>();
                if (value != null)
                {
                    outputParamter.Add(field.Key, ConvertType(value, field.Type));
                }
            }
        }
        else if (expression.ExpressionType == WorkflowFieldExpressionType.StringInterpolation)
        {
            // 字符串插值表达式
            var format = Smart.Format(expression.ExpressionValue, inputParamters);
            outputParamter.Add(field.Key, format);
        }
        else if (expression.ExpressionType == WorkflowFieldExpressionType.Regex)
        {
            // 正则表达式处理
            var regex = new Regex(expression.ExpressionValue);
            var match = regex.Match(inputJsonObject.ToString());
            if (match.Success)
            {
                outputParamter.Add(field.Key, match.Value);
            }
        }

        return outputParamter;
    }

    public static object ConvertType(object readValue, WorkflowFieldType fieldType)
    {
        if (fieldType == WorkflowFieldType.String)
        {
            return Convert.ToString(readValue)!;
        }

        if (fieldType == WorkflowFieldType.Number)
        {
            return Convert.ToDouble(readValue);
        }

        if (fieldType == WorkflowFieldType.Boolean)
        {
            return Convert.ToBoolean(readValue);
        }

        if (fieldType == WorkflowFieldType.Integer)
        {
            return Convert.ToInt32(readValue);
        }

        if (fieldType == WorkflowFieldType.Object)
        {
            return readValue; // 直接返回对象
        }

        if (fieldType == WorkflowFieldType.Array)
        {
            return readValue; // 直接返回数组
        }

        if (fieldType == WorkflowFieldType.Map)
        {
        }

        return null!;
    }

    public static string ConvertFixedJsonOutput(string inputJson, IReadOnlyCollection<WorkflowFieldDefinition> fieldDefinitions, IReadOnlyDictionary<string, object> otherVariables)
    {
        // 输入参数生成两种处理形式.
        JObject nodeJsonObject = JObject.Parse(inputJson);
        byte[] byteArray = Encoding.UTF8.GetBytes(s: inputJson);
        var inputParamters = ReadJsonHelper.Read(new ReadOnlySequence<byte>(byteArray), new System.Text.Json.JsonReaderOptions { MaxDepth = 3 });

        foreach (var item in otherVariables)
        {
            inputParamters.TryAdd(item.Key, item.Value);
            nodeJsonObject.Add(item.Key, JToken.FromObject(item.Value));
        }

        // 要生成的输出参数
        Dictionary<string, object> outputParamter = new Dictionary<string, object>();

        // 读取第一层要输出的参数
        Queue<WorkflowFieldDefinition> fieldsDefinitions = new Queue<WorkflowFieldDefinition>(fieldDefinitions);
        foreach (var item in fieldDefinitions)
        {
            fieldsDefinitions.Enqueue(item);
        }

        while (fieldsDefinitions.Count > 0)
        {
            var field = fieldsDefinitions.Dequeue();

            // 如果其本身设置了表达式，则不再处理子层，或者该字段没有下一层.
            if (field.Expression.ExpressionType != WorkflowFieldExpressionType.Fixed)
            {
                var parseValues = WorkflowValueHelper.ParseFieldValue(nodeJsonObject, inputParamters, field);
                if (parseValues.Count > 0)
                {
                    foreach (var item in parseValues)
                    {
                        outputParamter.Add(item.Key, item.Value);
                    }
                }

                continue;
            }

            // 判断有没有下一层，如果有则只处理下一层
            else if (field.Expression.ExpressionType == WorkflowFieldExpressionType.Fixed)
            {
                if (field.Children.Count > 0)
                {
                    foreach (var item in field.Children)
                    {
                        fieldsDefinitions.Enqueue(item);
                    }
                }
            }
            else
            {
                continue;
            }
        }

        return JsonParseHelper.Write(outputParamter);
    }
}

using Jint;
using System.Text;

namespace MoAI.Workflow.Engines;

/// <summary>
/// JavaScript 执行引擎，用于执行工作流中的 JavaScript 代码.
/// </summary>
public class WorkflowJavaScriptAction
{
    private const string _defaultScript =
        """
        function __invoke__(paramter, sys, g) {
          var pObj = JSON.parse(paramter);
          var sObj = JSON.parse(sys);
          var gObj = JSON.parse(g);
          var result = run(pObj, sObj, gObj);

          if(result !== undefined) {
            return JSON.stringify(result);
          }

          return '{}';
        }
        """;

    /*
     * 示例：
        function __invoke__(paramter, sys, g) {
          var pObj = JSON.parse(paramter);
          var sObj = JSON.parse(sys);
          var gObj = JSON.parse(g);
          var result = run(pObj, sObj, gObj);

          if(result !== undefined) {
            return JSON.stringify(result);
          }

          return '{}';
        }

function run(paramter, sys, g) {
  paramter.Id = 666;
  paramter.Name = "Jane Doe";

    paramter;
}
     */

    public string Inovke(string javascript, string jsonParamter, string systemParamter, string globalParamter, CancellationToken cancellationToken)
    {
        StringBuilder javascriptCode = new StringBuilder();
        javascriptCode.AppendLine(_defaultScript);
        javascriptCode.AppendLine(javascript);

        using var engine = new Engine(options =>
        {
            // Limit memory allocations to 4 MB
            options.LimitMemory(4_000_000);

            // Limit the recursion depth to 100 calls.
            options.LimitRecursion(100);

            // Set a timeout to 4 seconds.
            options.TimeoutInterval(TimeSpan.FromSeconds(4));

            // Set limit of 1000 executed statements.
            options.MaxStatements(1000);

            // Use a cancellation token.
            options.CancellationToken(cancellationToken);
        });

        var obj = engine.Execute(javascriptCode.ToString())
            .Invoke("__invoke__", jsonParamter, systemParamter, globalParamter);

        return obj.AsString();
    }
}
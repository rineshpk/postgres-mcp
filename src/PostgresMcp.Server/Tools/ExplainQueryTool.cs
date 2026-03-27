using System.ComponentModel;
using ModelContextProtocol.Server;
using PostgresMcp.Server.Models;
using PostgresMcp.Server.Services;

namespace PostgresMcp.Server.Tools;
[McpServerToolType]
public class ExplainQueryTool(QueryService service)
{
    private readonly QueryService _service = service;

    [McpServerTool]
    [Description("Executes an EXPLAIN query and returns the execution plan.")]
    public Task<ExplainResult> Execute(string query, int limit = 100)
        => _service.ExplainAsync(query);
}

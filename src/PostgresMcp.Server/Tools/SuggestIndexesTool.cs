using System.ComponentModel;
using ModelContextProtocol.Server;
using PostgresMcp.Server.Services;

namespace PostgresMcp.Server.Tools;
[McpServerToolType]
public class SuggestIndexesTool(QueryService service)
{
    private readonly QueryService _service = service;

    [McpServerTool]
    [Description("Suggests indexes for a given SQL query.")]
    public object SuggestIndexes(string query)
        => _service.SuggestIndexes(query);
}

using System.ComponentModel;
using ModelContextProtocol.Server;
using PostgresMcp.Server.Models;
using PostgresMcp.Server.Services;

namespace PostgresMcp.Server.Tools;

[McpServerToolType]
public class GetLocksTool(QueryService service)
{
    private readonly QueryService _service = service;

    [McpServerTool]
    [Description("Shows currently blocked queries along with the blocking query and PID. Returns an empty result when there are no lock conflicts.")]
    public Task<QueryResult> GetLocks()
        => _service.GetLocksAsync();
}

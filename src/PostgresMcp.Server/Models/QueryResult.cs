namespace PostgresMcp.Server.Models;

public record QueryResult(
    List<Dictionary<string, object>> Rows,
    int Count,
    double ExecutionTimeMs
);
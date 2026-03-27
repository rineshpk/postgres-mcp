namespace PostgresMcp.Server.Validators;

public static class SqlSafetyValidator
{
    private static readonly string[] Forbidden =
    [
        "INSERT", "UPDATE", "DELETE",
        "DROP", "ALTER", "TRUNCATE", "CREATE"
    ];

    public static void Validate(string query)
    {
        var upper = query.ToUpperInvariant();

        if (!upper.StartsWith("SELECT") && !upper.StartsWith("EXPLAIN"))
            throw new Exception("Only SELECT/EXPLAIN allowed");

        if (Forbidden.Any(f => upper.Contains(f)))
            throw new Exception("Forbidden SQL detected");

        if (upper.Contains(";"))
            throw new Exception("Multiple statements not allowed");
    }
}
using PostgresMcp.Server.Services;

namespace PostgresMcp.Tests;

public class SuggestIndexesTests
{
    // QueryService requires a connection string but SuggestIndexes never opens a connection.
    private static readonly QueryService Service = new("Host=unused");

    [Fact]
    public void SuggestIndexes_SingleWhereEquals_SuggestsOneIndex()
    {
        var result = Service.SuggestIndexes("SELECT * FROM orders WHERE status = 'active'");
        var suggestions = GetSuggestions(result);

        Assert.Single(suggestions);
        Assert.Contains("status", suggestions[0]);
    }

    [Fact]
    public void SuggestIndexes_MultipleWhereEquals_SuggestsAllColumns()
    {
        var result = Service.SuggestIndexes("SELECT * FROM orders WHERE user_id = 1 AND status = 'active'");
        var suggestions = GetSuggestions(result);

        Assert.Equal(2, suggestions.Count);
        Assert.Contains(suggestions, s => s.Contains("user_id"));
        Assert.Contains(suggestions, s => s.Contains("status"));
    }

    [Fact]
    public void SuggestIndexes_DuplicateColumns_DeduplicatesSuggestions()
    {
        var result = Service.SuggestIndexes("SELECT * FROM t WHERE id = 1 AND id = 2");
        var suggestions = GetSuggestions(result);

        Assert.Single(suggestions);
        Assert.Contains("id", suggestions[0]);
    }

    [Fact]
    public void SuggestIndexes_NoWhereClause_ReturnsEmptySuggestions()
    {
        var result = Service.SuggestIndexes("SELECT * FROM users");
        var suggestions = GetSuggestions(result);

        Assert.Empty(suggestions);
    }

    [Fact]
    public void SuggestIndexes_WhereClauseIsLowercase_StillExtractsColumns()
    {
        var result = Service.SuggestIndexes("SELECT * FROM orders where user_id = 42");
        var suggestions = GetSuggestions(result);

        Assert.Single(suggestions);
        Assert.Contains("user_id", suggestions[0]);
    }

    [Fact]
    public void SuggestIndexes_SuggestionsContainCreateIndexStatement()
    {
        var result = Service.SuggestIndexes("SELECT * FROM products WHERE category_id = 5");
        var suggestions = GetSuggestions(result);

        Assert.Single(suggestions);
        Assert.StartsWith("CREATE INDEX", suggestions[0]);
        Assert.Contains("category_id", suggestions[0]);
    }

    private static List<string> GetSuggestions(object result)
    {
        // SuggestIndexes returns an anonymous { suggestions } object
        var prop = result.GetType().GetProperty("suggestions")!;
        var enumerable = (IEnumerable<string>)prop.GetValue(result)!;
        return enumerable.ToList();
    }
}

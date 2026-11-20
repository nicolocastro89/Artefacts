namespace Artefacts.Infrastructure.Read;

/// <summary>Loads read-view DDL embedded from <c>Read/Views/*.sql</c> (used by migrations).</summary>
public static class ViewSql
{
    public static string Load(string viewName)
    {
        using var stream = typeof(ViewSql).Assembly.GetManifestResourceStream($"Views.{viewName}.sql")
                           ?? throw new InvalidOperationException($"View SQL '{viewName}.sql' is not embedded.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

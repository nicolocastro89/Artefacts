namespace Artefacts.Domain.Common;

internal static class FileNames
{
    /// <summary>
    /// The file name of a path as a browser may send it, whichever system it came from: what follows the last <c>/</c> or
    /// <c>\</c>. (<c>Path.GetFileName</c> only knows the separators of the server's own system.)
    /// </summary>
    public static string Leaf(string? path)
    {
        var text = path ?? "";
        return text[(text.LastIndexOfAny(['/', '\\']) + 1)..];
    }
}

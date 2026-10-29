using System.Diagnostics.CodeAnalysis;

namespace MetroCIM.Services;

public static class IfcExportPath
{
    public static string ExportName(string path)
    {
        string name = Path.GetFileNameWithoutExtension(path);
        return string.IsNullOrWhiteSpace(name) ? "export" : name;
    }

    public static string FileTypeOption(string path)
    {
        string extension = Path.GetExtension(path);
        if (extension.Equals(".ifcxml", StringComparison.OrdinalIgnoreCase))
            return "1";
        if (extension.Equals(".ifczip", StringComparison.OrdinalIgnoreCase))
            return "2";
        return "0";
    }

    public static string? FindWrittenFile(string requestedPath)
    {
        if (HasContent(requestedPath))
            return requestedPath;

        string? folder = Path.GetDirectoryName(requestedPath);
        if (string.IsNullOrWhiteSpace(folder))
            return File.Exists(requestedPath) ? requestedPath : null;

        string name = ExportName(requestedPath);
        string extension = Path.GetExtension(requestedPath);
        if (string.IsNullOrWhiteSpace(extension))
            extension = ".ifc";

        foreach (string candidate in Candidates(folder, name, extension))
        {
            if (HasContent(candidate))
                return candidate;
        }

        return File.Exists(requestedPath) ? requestedPath : null;
    }

    public static bool IsEmpty([NotNullWhen(false)] string? path) =>
        string.IsNullOrWhiteSpace(path) || !HasContent(path);

    public static IEnumerable<string> Candidates(string folder, string name, string extension)
    {
        yield return Path.Combine(folder, name + extension);
        yield return Path.Combine(folder, name + extension + extension);
        yield return Path.Combine(folder, name);
        yield return Path.Combine(folder, name + ".ifc");
        yield return Path.Combine(folder, name + ".ifcXML");
        yield return Path.Combine(folder, name + ".ifcZIP");
    }

    private static bool HasContent(string path)
    {
        try
        {
            return File.Exists(path) && new FileInfo(path).Length > 0;
        }
        catch (IOException)
        {
            return false;
        }
    }
}

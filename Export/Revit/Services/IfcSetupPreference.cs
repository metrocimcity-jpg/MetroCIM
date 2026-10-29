namespace MetroCIM.Services;

internal static class IfcSetupPreference
{
    public static string FilePath { get; } = Path.Combine(
        Path.GetDirectoryName(typeof(IfcSetupPreference).Assembly.Location)!,
        "last-ifc-setup.txt");

    public static string? Read()
    {
        try
        {
            return File.Exists(FilePath) ? File.ReadAllText(FilePath).Trim() : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static void Write(string name)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, name);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

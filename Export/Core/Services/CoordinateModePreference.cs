using MetroCIM.Models;

namespace MetroCIM.Services;

public static class CoordinateModePreference
{
    public const string FileName = "last-coordinate-mode.txt";

    public static CoordinateMode? TryParse(string? text)
    {
        if (text is null || string.IsNullOrWhiteSpace(text))
            return null;

        string value = text.Trim();
        if (string.Equals(value, "Local", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "LocalLocation", StringComparison.OrdinalIgnoreCase))
        {
            return CoordinateMode.Local;
        }

        if (string.Equals(value, "Shared", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "SharedCoordination", StringComparison.OrdinalIgnoreCase))
        {
            return CoordinateMode.Shared;
        }

        return null;
    }

    public static string Format(CoordinateMode mode) =>
        mode == CoordinateMode.Local ? "Local" : "Shared";

    public static CoordinateMode Read(string directory, CoordinateMode fallback = CoordinateMode.Local)
    {
        try
        {
            string path = Path.Combine(directory, FileName);
            if (!File.Exists(path))
                return fallback;

            return TryParse(File.ReadAllText(path)) ?? fallback;
        }
        catch (IOException)
        {
            return fallback;
        }
        catch (UnauthorizedAccessException)
        {
            return fallback;
        }
    }

    public static void Write(string directory, CoordinateMode mode)
    {
        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, FileName), Format(mode));
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

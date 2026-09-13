using System.Diagnostics;
using System.Text;

namespace MetroCIM.Services;

public sealed class XktConverter
{
    public const string InstallHint = "npm install -g @xeokit/xeokit-convert";

    public XktToolStatus Detect()
    {
        bool hasNode = FindOnPath("node.exe") is not null || FindOnPath("node") is not null;
        if (ResolveStartInfo("placeholder.glb", "placeholder.xkt", null) is not null)
            return XktToolStatus.Available;

        return hasNode ? XktToolStatus.MissingXeokitConvert : XktToolStatus.MissingNode;
    }

    public XktConversionResult Convert(
        string gltfPath,
        string xktPath,
        string? metadataPath = null,
        Action? heartbeat = null)
    {
        ProcessStartInfo? startInfo = ResolveStartInfo(gltfPath, xktPath, metadataPath);
        if (startInfo is null)
        {
            XktToolStatus status = Detect();
            string message = status == XktToolStatus.MissingNode
                ? "Node.js was not found on PATH. Install Node.js, then run:\n" + InstallHint
                : "@xeokit/xeokit-convert was not found. Install it with:\n" + InstallHint;

            return XktConversionResult.Failed(message);
        }

        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        startInfo.StandardOutputEncoding = Encoding.UTF8;
        startInfo.StandardErrorEncoding = Encoding.UTF8;

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
                return XktConversionResult.Failed("Failed to start xeokit-convert.");

            Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
            Task<string> stderrTask = process.StandardError.ReadToEndAsync();
            while (!process.WaitForExit(250))
                heartbeat?.Invoke();

            string stdout = stdoutTask.GetAwaiter().GetResult();
            string stderr = stderrTask.GetAwaiter().GetResult();

            bool wrote = File.Exists(xktPath) && new FileInfo(xktPath).Length > 0;
            if (wrote)
                return XktConversionResult.Succeeded(xktPath);

            string detail = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
            if (string.IsNullOrWhiteSpace(detail))
                detail = $"xeokit-convert exited with code {process.ExitCode}.";
            return XktConversionResult.Failed(detail.Trim());
        }
        catch (Exception ex)
        {
            return XktConversionResult.Failed(ex.Message);
        }
    }

    private static ProcessStartInfo? ResolveStartInfo(string gltfPath, string xktPath, string? metadataPath)
    {
        string arguments = $"-s \"{gltfPath}\" -o \"{xktPath}\" -e 1";
        if (!string.IsNullOrWhiteSpace(metadataPath))
            arguments += $" -m \"{metadataPath}\"";

        string? script = FindConvertScript();
        string? node = FindOnPath("node.exe") ?? FindOnPath("node");
        if (node is not null && script is not null)
        {
            return new ProcessStartInfo
            {
                FileName = node,
                Arguments = $"--max-old-space-size=16384 \"{script}\" {arguments}"
            };
        }

        string? cli = FindOnPath("xeokit-convert.cmd")
                      ?? FindOnPath("xeokit-convert.exe")
                      ?? FindOnPath("xeokit-convert")
                      ?? FindOnPath("convert2xkt.cmd")
                      ?? FindOnPath("convert2xkt");

        if (cli is null)
            return null;

        if (cli.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) ||
            cli.EndsWith(".bat", StringComparison.OrdinalIgnoreCase))
        {
            return new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"\"{cli}\" {arguments}\""
            };
        }

        return new ProcessStartInfo
        {
            FileName = cli,
            Arguments = arguments
        };
    }

    private static string? FindConvertScript()
    {
        string? npmRoot = TryReadProcessOutput("npm.cmd", "root -g") ?? TryReadProcessOutput("npm", "root -g");
        if (!string.IsNullOrWhiteSpace(npmRoot))
        {
            string candidate = Path.Combine(npmRoot.Trim(), "@xeokit", "xeokit-convert", "convert2xkt.js");
            if (File.Exists(candidate))
                return candidate;
        }

        string appDataNpm = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "npm",
            "node_modules",
            "@xeokit",
            "xeokit-convert",
            "convert2xkt.js");

        return File.Exists(appDataNpm) ? appDataNpm : null;
    }

    private static string? FindOnPath(string fileName)
    {
        if (File.Exists(fileName))
            return Path.GetFullPath(fileName);

        string[]? paths = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator);
        if (paths is null)
            return null;

        foreach (string path in paths)
        {
            if (string.IsNullOrWhiteSpace(path))
                continue;

            string candidate = Path.Combine(path.Trim('"'), fileName);
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    private static string? TryReadProcessOutput(string fileName, string arguments)
    {
        string? resolved = FindOnPath(fileName);
        if (resolved is null)
            return null;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = resolved.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) ? "cmd.exe" : resolved,
                Arguments = resolved.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase)
                    ? $"/c \"\"{resolved}\" {arguments}\""
                    : arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process is null)
                return null;

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(15000);
            return process.ExitCode == 0 ? output : null;
        }
        catch
        {
            return null;
        }
    }
}

public enum XktToolStatus
{
    Available,
    MissingNode,
    MissingXeokitConvert
}

public sealed record XktConversionResult(bool Success, string? Path, string? Error)
{
    public static XktConversionResult Succeeded(string path) => new(true, path, null);
    public static XktConversionResult Failed(string error) => new(false, null, error);
}

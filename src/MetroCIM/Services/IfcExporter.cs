using Autodesk.Revit.DB;
using MetroCIM.Models;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;

namespace MetroCIM.Services;

public sealed class IfcExporter
{
    public const string MissingExporterMessage =
        "Revit's IFC exporter was not found. Install or repair the Revit IFC add-in, then use File > Export > IFC once so its setups are available.";

    private readonly IReadOnlyList<(string Name, object Config)> _setups;

    private IfcExporter(IReadOnlyList<(string Name, object Config)> setups)
    {
        _setups = setups;
    }

    public IReadOnlyList<string> SetupNames => _setups.Select(s => s.Name).ToList();

    public static bool TryCreate(Document document, out IfcExporter exporter, out string error)
    {
        exporter = null!;
        error = MissingExporterMessage;

        Assembly? assembly;
        try
        {
            assembly = FindIfcUiAssembly(document);
        }
        catch (Exception ex)
        {
            error = $"Revit's IFC exporter could not be loaded.\n{ex.Message}";
            return false;
        }

        if (assembly is null)
        {
            error = "Revit's IFC exporter was not found next to Revit.exe.\nExpected:\n" +
                    string.Join("\n", CandidateDllPaths(document));
            return false;
        }

        Type? mapType = FindType(assembly, "IFCExportConfigurationsMap");
        Type? configType = FindType(assembly, "IFCExportConfiguration");
        if (mapType is null || configType is null)
        {
            error = $"Revit's IFC exporter loaded ({assembly.GetName().Name}) but setup types were not found.";
            return false;
        }

        SetStaticProperty(FindType(assembly, "IFCCommandOverrideApplication"), "TheDocument", document);

        object? map;
        try
        {
            map = CreateMap(mapType, document);
        }
        catch (Exception ex)
        {
            error = $"Could not create the IFC setup list.\n{ex.Message}";
            return false;
        }

        if (map is null)
            return false;

        object? inSession = Invoke(configType, "GetInSession", null, []);
        if (inSession is not null)
            Invoke(mapType, "AddOrReplace", map, [inSession]);

        Invoke(mapType, "AddBuiltInConfigurations", map, []);

        object? lastSelected = GetStaticProperty(FindType(assembly, "IFCExport"), "LastSelectedConfig");
        if (!TryInvoke(mapType, "AddSavedConfigurations", map, lastSelected is null ? [] : [lastSelected]) &&
            !TryInvoke(mapType, "AddSavedConfigurations", map, [document]) &&
            !TryInvoke(mapType, "AddSavedConfigurations", map, []))
        {
            error = "Could not read saved IFC setups from this model.";
            return false;
        }

        var setups = new List<(string Name, object Config)>();
        object? values = mapType.GetProperty("Values")?.GetValue(map) ?? map;
        if (values is IEnumerable enumerable)
        {
            foreach (object item in enumerable)
            {
                object config = item;
                PropertyInfo? valueProperty = item.GetType().GetProperty("Value");
                if (valueProperty is not null)
                    config = valueProperty.GetValue(item) ?? item;

                if (config.GetType().GetProperty("Name")?.GetValue(config) is string name &&
                    !string.IsNullOrWhiteSpace(name))
                {
                    setups.Add((name, config));
                }
            }
        }

        if (setups.Count == 0)
        {
            error = "No Revit IFC setups were found.";
            return false;
        }

        exporter = new IfcExporter(setups);
        error = string.Empty;
        return true;
    }

    public IfcExportOutcome Export(Document document, View view, string path, string setupName)
    {
        object config = _setups.First(s => s.Name == setupName).Config;
        ElementId activeViewId = view.Id;
        SetActiveViewId(config, GetBool(config, "UseActiveViewGeometry") ? activeViewId : ElementId.InvalidElementId);

        var options = new IFCExportOptions();
        if (!ApplyOptions(config, document, options, activeViewId))
        {
            return IfcExportOutcome.Failed(
                setupName,
                "The selected IFC setup could not be applied to Revit's IFC export options.");
        }

        return WriteIfc(document, view, path, setupName, options);
    }

    public static IfcExportOutcome ExportVisibleView(Document document, View view, string path)
    {
        const string setupName = "IFC4 Reference View";
        var options = new IFCExportOptions
        {
            FileVersion = IFCVersion.IFC4RV,
            FilterViewId = ElementId.InvalidElementId,
            ExportBaseQuantities = true
        };
        options.AddOption("VisibleElementsOfCurrentView", "false");
        options.AddOption("UseActiveViewGeometry", "true");
        options.AddOption("ExportRoomsInView", "true");
        return WriteIfc(document, view, path, setupName, options);
    }

    private static IfcExportOutcome WriteIfc(
        Document document,
        View view,
        string path,
        string setupName,
        IFCExportOptions options)
    {
        string folder = Path.GetDirectoryName(path)!;
        string name = IfcExportPath.ExportName(path);
        Directory.CreateDirectory(folder);
        NormalizeOptions(options, view, path);

        using var transaction = new Transaction(document, "Export IFC");
        FailureHandlingOptions failure = transaction.GetFailureHandlingOptions();
        failure.SetClearAfterRollback(false);
        transaction.SetFailureHandlingOptions(failure);
        transaction.Start();

        bool appliedAppearance = false;
        Dictionary<string, ResolvedAppearance> colorsByIfcGuid = [];
        bool exported;
        try
        {
            colorsByIfcGuid = IfcAppearanceApplier.Apply(document, view);
            appliedAppearance = colorsByIfcGuid.Count > 0;
            exported = document.Export(folder, name, options);
            if (IfcExportPath.IsEmpty(IfcExportPath.FindWrittenFile(path)))
                exported = document.Export(folder, Path.GetFileName(path), options);
            if (IfcExportPath.IsEmpty(IfcExportPath.FindWrittenFile(path)) &&
                options.FilterViewId != ElementId.InvalidElementId)
            {
                ClearViewFilter(options);
                exported = document.Export(folder, name, options);
            }

            IfcAppearanceApplier.HarvestStoredGuids(document, view, colorsByIfcGuid);
        }
        catch (Exception ex)
        {
            if (transaction.GetStatus() == TransactionStatus.Started)
                transaction.RollBack();
            return IfcExportOutcome.Failed(setupName, ex.Message);
        }

        string? written = IfcExportPath.FindWrittenFile(path);
        string? snapshot = null;
        if (!IfcExportPath.IsEmpty(written))
        {
            snapshot = Path.Combine(folder, name + ".metro-cim-ifc.tmp");
            try
            {
                File.Copy(written, snapshot, overwrite: true);
            }
            catch (IOException)
            {
                snapshot = null;
            }
        }

        if (exported && !appliedAppearance)
            transaction.Commit();
        else
            transaction.RollBack();

        try
        {
            if (snapshot is not null && File.Exists(snapshot))
            {
                File.Copy(snapshot, path, overwrite: true);
                TryDelete(snapshot);
            }
            else if (!IfcExportPath.IsEmpty(written) &&
                     !string.Equals(written, path, StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(written, path, overwrite: true);
            }
        }
        catch (Exception ex)
        {
            return IfcExportOutcome.Failed(setupName, ex.Message);
        }

        if (!exported)
            return IfcExportOutcome.Failed(setupName, "Revit did not write the IFC file.");

        if (IfcExportPath.IsEmpty(path))
        {
            return IfcExportOutcome.Failed(
                setupName,
                "Revit created an empty IFC file. Close this add-in dialog and use File > Export > IFC with the same setup to confirm the exporter, then try MetroCIM again.");
        }

        try
        {
            IfcColorPatcher.Apply(path, colorsByIfcGuid);
        }
        catch (Exception ex)
        {
            return IfcExportOutcome.Failed(setupName, "IFC was written but pipe colors could not be applied.\n" + ex.Message);
        }

        return IfcExportOutcome.Succeeded(setupName, path);
    }

    private static void NormalizeOptions(IFCExportOptions options, View view, string path)
    {
        if ((int)options.FileVersion == 0)
            options.FileVersion = IFCVersion.IFC4RV;

        options.AddOption("ActiveViewId", view.Id.Value.ToString());
        options.AddOption("TessellationLevelOfDetail", "0.8");
        options.AddOption("IFCFileType", IfcExportPath.FileTypeOption(path));

        try
        {
            if (!string.IsNullOrWhiteSpace(options.FamilyMappingFile) &&
                !File.Exists(options.FamilyMappingFile))
            {
                options.FamilyMappingFile = string.Empty;
            }
        }
        catch (Autodesk.Revit.Exceptions.ApplicationException)
        {
        }
    }

    private static void ClearViewFilter(IFCExportOptions options)
    {
        options.FilterViewId = ElementId.InvalidElementId;
        options.AddOption("VisibleElementsOfCurrentView", "false");
    }

    private static void TryDelete(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
    }

    private static bool _resolverHooked;
    private static readonly List<string> ProbeDirectories = [];

    private static void EnsureAssemblyResolver(Document document)
    {
        foreach (string candidate in CandidateDllPaths(document))
        {
            string? directory = Path.GetDirectoryName(candidate);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                ProbeDirectories.Add(directory);
        }

        try
        {
            string? revitExe = Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrWhiteSpace(revitExe))
                ProbeDirectories.Add(Path.GetDirectoryName(revitExe)!);
        }
        catch (Exception)
        {
        }

        if (_resolverHooked)
            return;

        _resolverHooked = true;
        AppDomain.CurrentDomain.AssemblyResolve += (_, args) => LoadFromProbe(new AssemblyName(args.Name).Name);
        AssemblyLoadContext? context = AssemblyLoadContext.GetLoadContext(typeof(IfcExporter).Assembly);
        if (context is not null)
            context.Resolving += (_, name) => LoadFromProbe(name.Name);
    }

    private static Assembly? LoadFromProbe(string? assemblyName)
    {
        if (string.IsNullOrWhiteSpace(assemblyName))
            return null;

        foreach (string directory in ProbeDirectories.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            string path = Path.Combine(directory, assemblyName + ".dll");
            if (!File.Exists(path))
                continue;

            try
            {
                return Assembly.LoadFrom(path);
            }
            catch (Exception)
            {
            }
        }

        return null;
    }

    private static Assembly? FindIfcUiAssembly(Document document)
    {
        EnsureAssemblyResolver(document);
        foreach (Assembly assembly in EnumerateAssemblies())
        {
            string? name = assembly.GetName().Name;
            if (name is null)
                continue;

            if (name.Equals("Autodesk.IFC.Export.UI", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("IFCExporterUIOverride", StringComparison.OrdinalIgnoreCase))
            {
                return assembly;
            }

            if (name.Contains("IFC", StringComparison.OrdinalIgnoreCase) &&
                FindType(assembly, "IFCExportConfigurationsMap") is not null)
            {
                return assembly;
            }
        }

        foreach (string path in CandidateDllPaths(document))
        {
            if (!File.Exists(path))
                continue;

            try
            {
                AssemblyLoadContext? context = AssemblyLoadContext.GetLoadContext(typeof(IfcExporter).Assembly);
                return context is not null
                    ? context.LoadFromAssemblyPath(path)
                    : Assembly.LoadFrom(path);
            }
            catch (Exception)
            {
                // Try the next candidate path.
            }
        }

        return null;
    }

    private static IEnumerable<string> CandidateDllPaths(Document document)
    {
        var directories = new List<string>();
        try
        {
            string? revitExe = Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrWhiteSpace(revitExe))
                directories.Add(Path.GetDirectoryName(revitExe)!);
        }
        catch (Exception)
        {
            // Main module path can be restricted.
        }

        directories.Add(@"C:\Program Files\Autodesk\Revit 2027");

        string version = document.Application.VersionNumber;
        directories.Add(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Autodesk",
            $"Revit {version}"));

        string? apiDir = Path.GetDirectoryName(typeof(Document).Assembly.Location);
        if (!string.IsNullOrWhiteSpace(apiDir))
            directories.Add(apiDir);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string directory in directories)
        {
            if (string.IsNullOrWhiteSpace(directory) || !seen.Add(directory))
                continue;

            yield return Path.Combine(directory, "AddIns", "IFCExporterUI", "Autodesk.IFC.Export.UI.dll");
            yield return Path.Combine(directory, "Autodesk.IFC.Export.UI.dll");
        }
    }

    private static IEnumerable<Assembly> EnumerateAssemblies()
    {
        foreach (AssemblyLoadContext context in AssemblyLoadContext.All)
        {
            foreach (Assembly assembly in context.Assemblies)
                yield return assembly;
        }
    }

    private static Type? FindType(Assembly assembly, string simpleName)
    {
        Type? exact = assembly.GetType($"BIM.IFC.Export.UI.{simpleName}")
                      ?? assembly.GetType($"Autodesk.IFC.Export.UI.{simpleName}");
        if (exact is not null)
            return exact;

        try
        {
            return assembly.GetTypes().FirstOrDefault(type => type.Name == simpleName);
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.FirstOrDefault(type => type?.Name == simpleName);
        }
    }

    private static object? CreateMap(Type mapType, Document document)
    {
        ConstructorInfo? withDocument = mapType.GetConstructor([typeof(Document)]);
        if (withDocument is not null)
            return withDocument.Invoke([document]);

        object? map = Activator.CreateInstance(mapType);
        mapType.GetProperty("Document")?.SetValue(map, document);
        return map;
    }

    private static bool ApplyOptions(object config, Document document, IFCExportOptions options, ElementId filterViewId)
    {
        foreach (MethodInfo method in config.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            if (method.Name != "UpdateOptions")
                continue;

            ParameterInfo[] parameters = method.GetParameters();
            try
            {
                if (parameters.Length == 4 &&
                    parameters[0].ParameterType == typeof(Document) &&
                    parameters[1].ParameterType == typeof(IFCExportOptions))
                {
                    foreach (bool mappingFlag in new[] { false, true })
                    {
                        try
                        {
                            method.Invoke(config, [document, options, filterViewId, mappingFlag]);
                            return true;
                        }
                        catch (TargetInvocationException)
                        {
                        }
                    }
                }

                if (parameters.Length == 3 &&
                    parameters[0].ParameterType == typeof(Document) &&
                    parameters[1].ParameterType == typeof(IFCExportOptions))
                {
                    method.Invoke(config, [document, options, filterViewId]);
                    return true;
                }

                if (parameters.Length == 2 &&
                    parameters[0].ParameterType == typeof(IFCExportOptions))
                {
                    method.Invoke(config, [options, filterViewId]);
                    return true;
                }
            }
            catch (TargetInvocationException)
            {
                // Try the next UpdateOptions overload.
            }
        }

        return false;
    }

    private static void SetActiveViewId(object config, ElementId viewId)
    {
        PropertyInfo? property = config.GetType().GetProperty("ActiveViewId");
        if (property is null || !property.CanWrite)
            return;

        if (property.PropertyType == typeof(ElementId))
            property.SetValue(config, viewId);
        else if (property.PropertyType == typeof(long))
            property.SetValue(config, viewId.Value);
        else if (property.PropertyType == typeof(int))
            property.SetValue(config, (int)viewId.Value);
    }

    private static bool GetBool(object target, string propertyName)
    {
        object? value = target.GetType().GetProperty(propertyName)?.GetValue(target);
        return value is true;
    }

    private static object? GetStaticProperty(Type? type, string propertyName)
    {
        return type?.GetProperty(propertyName)?.GetValue(null);
    }

    private static void SetStaticProperty(Type? type, string propertyName, object? value)
    {
        type?.GetProperty(propertyName)?.SetValue(null, value);
    }

    private static object? Invoke(Type type, string name, object? instance, object?[] args)
    {
        TryInvoke(type, name, instance, args, out object? result);
        return result;
    }

    private static bool TryInvoke(Type type, string name, object? instance, object?[] args) =>
        TryInvoke(type, name, instance, args, out _);

    private static bool TryInvoke(Type type, string name, object? instance, object?[] args, out object? result)
    {
        result = null;
        BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        foreach (MethodInfo method in type.GetMethods(flags))
        {
            if (method.Name != name)
                continue;

            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length != args.Length)
                continue;

            try
            {
                result = method.Invoke(instance, args);
                return true;
            }
            catch (TargetInvocationException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                // Wrong overload.
            }
        }

        return false;
    }
}

public readonly record struct IfcExportOutcome(bool Success, string SetupName, string? Path, string? Error)
{
    public static IfcExportOutcome Succeeded(string setupName, string path) =>
        new(true, setupName, path, null);

    public static IfcExportOutcome Failed(string setupName, string error) =>
        new(false, setupName, null, error);
}
